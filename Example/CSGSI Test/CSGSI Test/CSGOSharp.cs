using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Threading;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace CSGSI
{
    /// <summary>
    /// Full class to use CSGO's Game State Integration.
    ///  https://developer.valvesoftware.com/wiki/Counter-Strike:_Global_Offensive_Game_State_Integration
    /// See: http://pastebin.com/d6hsmV89 for all variables and their values.
    /// There are some processed values such as round timers/bomb timers.
    /// Always run the application with adminitrator rights, when debugging, run Visual Studio as administrator.
    ///  Get started: 
    ///     - Add: http://pastebin.com/4MwDZ9Um to Counter-Strike Global Offensive\csgo\cfg\gamestate_integration_test.cfg
    ///     - Add: using CSGSI;
    ///  - Download Newtonsoft.Json from NuGet
    ///        - For event handeler: 
    ///             CSGOSharp.NewGameState += new CSGOSharp.NewGameStateHandler(onNewgameState);
    ///             private void onNewgameState() { }
    ///        - To start: CSGOSharp.Start();
    ///        - To stop: CSGOSharp.Stop();
    /// </summary>
    /*
           
        */
    class CSGOSharp
    {
        /// <summary>
        /// Used to start this service. Will return true if succesfull. Always run the application with adminitrator rights.
        /// </summary>
        public static bool Start(int port)
        {
            backgroundWorkerServer.WorkerSupportsCancellation = true;
            backgroundWorkerServer.DoWork += new DoWorkEventHandler(backgroundWorkerServer_DoWork);
            backgroundWorkerPlayers.WorkerSupportsCancellation = true;
            backgroundWorkerPlayers.DoWork += new DoWorkEventHandler(backgroundWorkerPlayer_DoWork);

            portNr = port;
            backgroundWorkerServer.RunWorkerAsync();

            timers.Add(new Timers());
            timers.Last<Timers>().name = "competitive";
            timers.Last<Timers>().bomb = 41;
            timers.Last<Timers>().roundSec = 55;
            timers.Last<Timers>().roundMin = 1;
            timers.Last<Timers>().freeze = 15;
            timers.Last<Timers>().endround = 7;

            timers.Add(new Timers());
            timers.Last<Timers>().name = "casual";
            timers.Last<Timers>().bomb = 41;
            timers.Last<Timers>().roundSec = 15;
            timers.Last<Timers>().roundMin = 2;
            timers.Last<Timers>().freeze = 5;
            timers.Last<Timers>().endround = 9;

            return true;
        }
        /// <summary>
        /// Used to stop this service. Will return true if succesfull. This will not stop it instantly as the input buffer has to be cleared.
        /// </summary>
        public static bool Stop()
        {
            backgroundWorkerPlayers.CancelAsync();
            backgroundWorkerServer.CancelAsync();
            GSIListener.Stop();
            return true;
        }

        private static BackgroundWorker backgroundWorkerServer = new BackgroundWorker();
        private static BackgroundWorker backgroundWorkerPlayers = new BackgroundWorker();

        /// <summary>
        /// Return JSON from server.
        /// </summary>
        public static dynamic JSON = null;
        /// <summary>
        /// List of all players available, while playing yourself this will be null.
        /// Always use in combination with bool listLoaded.
        /// </summary>
        public static List<Player> players = new List<Player>();
        /// <summary>
        /// Player you are playing as or player that you are spectating.
        /// </summary>
        public static Player currentPlayer = new Player();
        /// <summary>
        /// Server info you are connected to.
        /// </summary>
        public static Server server = new Server();
        /// <summary>
        /// List of all players names available, while playing yourself this will be null.
        /// Always use in combination with bool listLoaded.
        /// </summary>
        public static List<string> playerNames = new List<string>();
        /// <summary>
        /// List of different timers, you can add/edit timers.
        /// </summary>
        public static List<Timers> timers = new List<Timers>();
        /// <summary>
        /// Bool for checking status.
        /// </summary>
        public static bool freeze = false, live = false, planted = false;
        /// <summary>
        /// Use this bool to check if the lists players and playerNames are filled. If you don't it can cause errors because we are reading/editing these lists in different threads.
        /// </summary>
        public static bool listLoaded = false;
        /// <summary>
        /// Time variables.
        /// </summary>
        public static int min = 1, sec = 55;
        /// <summary>
        /// Counts the recieved POST's between timestamps.
        /// </summary>
        public static int subtime = 0;
        /// <summary>
        /// For string dumpSelectedBuffer.
        /// </summary>
        public static int dumpPlayerIndex = 0;
        /// <summary>
        /// Full time string of roundtime.
        /// </summary>
        public static string timeString = "";
        /// <summary>
        /// Returns a printable version if you would need a kit to defuse.
        /// </summary>
        public static string kitornot = "";
        /// <summary>
        /// Dump all variables in printable string.
        /// </summary>
        public static string dumpSelectedBuffer = "", dumpCurrentPLayer = "", dumpServerStats = "";

        private static int portNr = 3000;
        private static string Sjson = "";
        private static double bufferTimeStamp = 0;
        private static bool playerdone = false, serverdone = false;

        /// <summary>
        /// Event handler for new game state.
        /// </summary>
        public delegate void NewGameStateHandler();
        /// <summary>
        /// Event for new game state.
        /// </summary>
        public static event NewGameStateHandler NewGameState;

        private static void timerReplaced()
        {
            try { dumpSelectedBuffer = CSGOSharp.dumpPlayer(players[dumpPlayerIndex]); } catch (Exception) { }

            if (bufferTimeStamp != server.provider.timestamp)
            {
                subtime = 0;

                if (sec <= 0 && min != 0)
                {
                    sec = 60;
                    min--;
                }
                if (min >= 0 && sec > 0 && server.map.phase == "live")
                {
                    sec = sec - Convert.ToInt32(server.provider.timestamp - bufferTimeStamp);
                }

                bufferTimeStamp = server.provider.timestamp;
            }
            else
            {
                subtime++;
            }


            if (server.round.phase == "freezetime" && !freeze)
            {
                freeze = true;
                live = false;

                foreach (Timers item in timers)
                {
                    if (server.map.mode == item.name)
                    {
                        min = 0;
                        sec = item.freeze;
                    }
                }

            }
            if (server.round.phase == "live" && freeze)
            {
                freeze = false;
                live = true;
                foreach (Timers item in timers)
                {
                    if (server.map.mode == item.name)
                    {
                        min = item.roundMin;
                        sec = item.roundSec;
                    }
                }

            }
            if (server.round.phase == "over" && live)
            {
                live = false;
                planted = false;

                foreach (Timers item in timers)
                {
                    if (server.map.mode == item.name)
                    {
                        min = 0;
                        sec = item.endround;
                    }
                }
            }
            if (server.round.bomb == "planted" && !planted && live)
            {
                foreach (Timers item in timers)
                {
                    if (server.map.mode == item.name)
                    {
                        min = 0;
                        sec = item.bomb;

                    }
                }

                planted = true;
            }
            if (planted)
            {

                if (sec <= 10)
                {
                    kitornot = "KIT";
                }
                else
                {
                    kitornot = "No Kit";
                }
                if (sec <= 5)
                {
                    kitornot = "RUN";
                }

            }
            else
            {
                kitornot = "Not planted";
            }

            try
            {
                currentPlayer = parseToPlayer(JSON.player);
                double health = currentPlayer.state.health * 2.55;
                // this.BackColor = Color.FromArgb(255-Convert.ToInt32(health), Convert.ToInt32(health), 0);

                dumpCurrentPLayer = dumpPlayer(currentPlayer);
            }
            catch (Exception)
            {

            }

            string times = ":";
            if (sec < 10)
            { times = ":0"; }
            timeString = min + times + sec;

        }
        private static void OnNewGameState(object sender, EventArgs e)
        {
            GameState gs = (GameState)sender;
            Sjson = gs.JSON;
            JSON = JsonConvert.DeserializeObject<dynamic>(Sjson);

            timerReplaced();

            try
            {

                if (!backgroundWorkerServer.IsBusy)
                { backgroundWorkerServer.RunWorkerAsync(); }
            }
            catch (Exception) { }

            try
            {
                if (!backgroundWorkerPlayers.IsBusy)
                { backgroundWorkerPlayers.RunWorkerAsync(); }
            }
            catch (Exception) { }
        }
        private static void backgroundWorkerServer_DoWork(object sender, DoWorkEventArgs e)
        {
            serverdone = false;
            if (!GSIListener.Running)
            {
                if (GSIListener.Start(portNr))
                {
                    GSIListener.NewGameState += new EventHandler(OnNewGameState);
                    // timer.Start();!timer.Enabled && 
                }
            }
            //label1.Text = Sjson;

            if (JSON != null)
            {
                server = CSGOSharp.parseToServer(JSON);
                dumpServerStats = "Timestamp: \r\n" + UnixTimeStampToDateTime(server.provider.timestamp)
                    + "\r\n" + server.provider.timestamp + "." + subtime + "\r\n"
            + "\r\nMap: " + server.map.name
            + "\r\nRound: " + (server.map.round + 1)
                + "\r\nState: " + server.round.phase + " " + server.map.phase + " " + server.map.team_ct.score + " - " + server.map.team_t.score
            + "\r\nBomb: " + server.round.bomb;
            }
            serverdone = true;
            check();
        }
        private static void backgroundWorkerPlayer_DoWork(object sender, DoWorkEventArgs e)
        {
            playerdone = false;
            try
            {
                if (JSON != null)
                {
                    Player player1 = new Player();

                    if (JSON.allplayers != null)
                    {
                        listLoaded = false;
                        playerNames = new List<string>();
                        players.Clear();
                        foreach (var item in JSON.allplayers)
                        {
                            player1 = new Player();
                            if (item == null)
                            {
                                return;
                            }

                            player1 = CSGOSharp.parseToPlayer(item.First);

                            players.Add(player1);
                            playerNames.Add(player1.name);

                        }
                        listLoaded = true;
                    }
                }


            }
            catch (Exception)
            {
            }
            playerdone = true;
            check();
        }
        private static void check()
        {
            if (playerdone && serverdone)
            {
                NewGameState();
            }
        }

        /// <summary>
        /// Used to convert the recieved unix timestamp to a printable date.
        /// </summary>
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
        /// <summary>
        /// Used to convert a player JSON Object to Player Object
        /// </summary>
        public static Player parseToPlayer(dynamic playerVar)
        {
            Player player1 = new Player();
            try { player1.steamid = playerVar.steamid; } catch (Exception) { }
            try { player1.name = playerVar.name; } catch (Exception) { }
            try { player1.team = playerVar.team; } catch (Exception) { }
            try { player1.activity = playerVar.activity; } catch (Exception) { }

            try { player1.match_stats.kills = playerVar.match_stats.kills; } catch (Exception) { }
            try { player1.match_stats.assists = playerVar.match_stats.assists; } catch (Exception) { }
            try { player1.match_stats.deaths = playerVar.match_stats.deaths; } catch (Exception) { }
            try { player1.match_stats.mvps = playerVar.match_stats.mvps; } catch (Exception) { }
            try { player1.match_stats.score = playerVar.match_stats.score; } catch (Exception) { }

            try { player1.state.health = playerVar.state.health; } catch (Exception) { }
            try { player1.state.armor = playerVar.state.armor; } catch (Exception) { }
            try { player1.state.helmet = playerVar.state.helmet; } catch (Exception) { }
            try { player1.state.flashed = playerVar.state.flashed; } catch (Exception) { }
            try { player1.state.smoked = playerVar.state.smoked; } catch (Exception) { }
            try { player1.state.burning = playerVar.state.burning; } catch (Exception) { }
            try { player1.state.money = playerVar.state.money; } catch (Exception) { }
            try { player1.state.round_kills = playerVar.state.round_kills; } catch (Exception) { }
            try { player1.state.round_killhs = playerVar.state.round_killhs; } catch (Exception) { }

            int count = 0;
            try
            {
                foreach (var item2 in playerVar.weapons)
                {
                    var tolist = item2.First;
                    try { player1.weapons.Add(new Weapon()); } catch (Exception) { }
                    try { player1.weapons[count].ammo_clip = tolist.ammo_clip; } catch (Exception) { }
                    try { player1.weapons[count].ammo_clip_max = tolist.ammo_clip_max; } catch (Exception) { }
                    try { player1.weapons[count].ammo_reserve = tolist.ammo_reserve; } catch (Exception) { }
                    try { player1.weapons[count].name = tolist.name; } catch (Exception) { }
                    try { player1.weapons[count].paintkit = tolist.paintkit; } catch (Exception) { }
                    try { player1.weapons[count].state = tolist.state; } catch (Exception) { }
                    try { player1.weapons[count].type = tolist.type; } catch (Exception) { }
                    count++;
                }
            }
            catch (Exception)
            {

            }
            return player1;
        }
        /// <summary>
        /// Used to convert a JSON Object to Server Object
        /// </summary>
        public static Server parseToServer(dynamic json)
        {
            Server dummy = new Server();
            try { dummy.map.mode = json.map.mode; } catch (Exception) { }
            try { dummy.map.name = json.map.name; } catch (Exception) { }
            try { dummy.map.phase = json.map.phase; } catch (Exception) { }
            try { dummy.map.round = json.map.round; } catch (Exception) { }
            JValue score = new JValue(0);
            try { score = json.map.team_ct.score; } catch (Exception) { }
            dummy.map.team_ct.score = Convert.ToInt32(score.Value);
            try { score = json.map.team_t.score; } catch (Exception) { }
            dummy.map.team_t.score = Convert.ToInt32(score.Value);
            try { dummy.map.team_t.score = json.map.team_t.score; } catch (Exception) { }
            try { dummy.provider.appid = json.provider.appid; } catch (Exception) { }
            try { dummy.provider.name = json.provider.name; } catch (Exception) { }
            try { dummy.provider.steamid = json.provider.steamid; } catch (Exception) { }
            try { dummy.provider.timestamp = json.provider.timestamp; } catch (Exception) { }
            try { dummy.provider.version = json.provider.version; } catch (Exception) { }

            try { dummy.round.phase = json.round.phase; } catch (Exception) { }
            try { dummy.round.bomb = json.round.bomb; } catch (Exception) { }
            try { dummy.round.win_team = json.round.win_team; } catch (Exception) { }
            return dummy;
        }

        /// <summary>
        /// Used to dump a player to a printable string.
        /// </summary>
        public static string dumpPlayer(Player player1)
        {
            string builder = "";

            //builder = "Specing player:\r\n";
            builder = "Steamid: " + player1.steamid.ToString(".#####################################################################################################################################################################################################################################################################################################################################");
            builder += "\r\nName: " + player1.name;
            builder += "\r\nTeam: " + player1.team;
            builder += "\r\nActivity: " + player1.activity;
            builder += "\r\nMatch stats: " + player1.match_stats.kills
                + " - " + player1.match_stats.assists
                + " - " + player1.match_stats.deaths
                + " - " + player1.match_stats.mvps
                + " - " + player1.match_stats.score;
            builder += "\r\n\r\nState:";
            builder += "\r\nHealth: " + player1.state.health;
            builder += "\r\nArmor: " + player1.state.armor;
            builder += "\r\nHelmet: " + player1.state.helmet;
            builder += "\r\nFlashed: " + player1.state.flashed;
            builder += "\r\nSmoked: " + player1.state.smoked;
            builder += "\r\nBurning: " + player1.state.burning;
            builder += "\r\nMoney: " + player1.state.money;
            builder += "\r\nRound_kills: " + player1.state.round_kills;
            builder += "\r\nRound_killhs: " + player1.state.round_killhs;
            builder += "\r\n\r\nWeapons: ";
            foreach (Weapon item in player1.weapons)
            {
                builder += "\r\n";
                if (item.state == "active")
                {
                    builder += "†";
                }
                if (item.type == "Pistol")
                {
                    builder += "Secundary: " + item.name.Remove(0, 7) + " " + item.ammo_clip + "/" + item.ammo_reserve;
                }
                else if (item.type == "Knife")
                {
                    builder += "Knife: " + item.name.Remove(0, 7) + " " + item.paintkit;
                }
                else if (item.type == "Rifle")
                {
                    builder += "Rifle: " + item.name.Remove(0, 7) + " " + item.ammo_clip + "/" + item.ammo_reserve;
                }
                else if (item.type == "Grenade")
                {
                    builder += "Nade: " + item.name.Remove(0, 7) + " " + item.ammo_reserve;
                }
                else if (item.type == "C4")
                {
                    builder += "C4: " + item.ammo_reserve;
                }
                else
                {
                    builder += "" + item.type + ": " + item.name.Remove(0, 7) + " " + item.ammo_clip + "/" + item.ammo_reserve;
                }

            }
            return builder;
        }
    }


    public class Player
    {
        public Player()
        {
            match_stats = new match_stats();
            state = new state();
            weapons = new List<Weapon>();
        }
        public double steamid { get; set; }
        public string name { get; set; }
        public string team { get; set; }
        public string activity { get; set; }
        public match_stats match_stats { get; set; }
        public state state { get; set; }
        public List<Weapon> weapons { get; set; }
    }

    public class match_stats
    {
        public int kills { get; set; }
        public int assists { get; set; }
        public int deaths { get; set; }
        public int mvps { get; set; }
        public int score { get; set; }
    }

    public class state
    {
        public int health { get; set; }
        public int armor { get; set; }
        public bool helmet { get; set; }
        public int flashed { get; set; }
        public int smoked { get; set; }
        public int burning { get; set; }
        public int money { get; set; }
        public int round_kills { get; set; }
        public int round_killhs { get; set; }
    }

    public class Weapon
    {
        public string name { get; set; }
        public string paintkit { get; set; }
        public string state { get; set; }
        public string type { get; set; }

        public int ammo_clip { get; set; }
        public int ammo_clip_max { get; set; }
        public int ammo_reserve { get; set; }
    }

    public class Server
    {
        public Server()
        {
            provider = new provider();
            map = new map();
            round = new round();
        }
        public provider provider { get; set; }
        public map map { get; set; }
        public round round { get; set; }
    }
    public class provider
    {
        public string name { get; set; }
        public int appid { get; set; }
        public int version { get; set; }
        public double steamid { get; set; }
        public double timestamp { get; set; }
    }
    public class map
    {
        public map()
        {
            team_t = new team();
            team_ct = new team();
        }
        public string mode { get; set; }
        public string name { get; set; }
        public string phase { get; set; }
        public int round { get; set; }
        public double timestamp { get; set; }
        public team team_t { get; set; }
        public team team_ct { get; set; }
    }

    public class team
    {
        public int score { get; set; }
    }

    public class round
    {
        public string phase { get; set; }
        public string bomb { get; set; }
        public string win_team { get; set; }
    }

    public class Timers
    {
        public string name { get; set; }
        public int freeze { get; set; }
        public int roundMin { get; set; }
        public int roundSec { get; set; }
        public int bomb { get; set; }
        public int endround { get; set; }
    }

    //Credit From this point to https://github.com/rakijah/CSGSI


    /// <summary>
    /// This object represents the entire game state 
    /// </summary>
    public class GameState
    {
        private JObject m_Data;

        private GameStateNode m_Provider;
        private GameStateNode m_Map;
        private GameStateNode m_Round;
        private GameStateNode m_Player;
        private GameStateNode m_Auth;
        private GameStateNode m_Added;
        private GameStateNode m_Previously;

        /// <summary>
        /// The "provider" subnode
        /// </summary>
        public GameStateNode Provider { get { return m_Provider; } }

        /// <summary>
        /// The "map" subnode
        /// </summary>
        public GameStateNode Map { get { return m_Map; } }

        /// <summary>
        /// The "round" subnode
        /// </summary>
        public GameStateNode Round { get { return m_Round; } }

        /// <summary>
        /// The "player" subnode
        /// </summary>
        public GameStateNode Player { get { return m_Player; } }

        /// <summary>
        /// The "auth" subnode
        /// </summary>
        public GameStateNode Auth { get { return m_Auth; } }

        /// <summary>
        /// The "added" subnode
        /// </summary>
        public GameStateNode Added { get { return m_Added; } }

        /// <summary>
        /// The "previously" subnode
        /// </summary>
        public GameStateNode Previously { get { return m_Previously; } }

        private string m_JSON;

        /// <summary>
        /// The JSON string that was used to generate this object
        /// </summary>
        public string JSON { get { return m_JSON; } }

        /// <summary>
        /// Initialises a new GameState object using a JSON string
        /// </summary>
        /// <param name="JSONstring"></param>
        public GameState(string JSONstring)
        {
            m_JSON = JSONstring;

            if (!JSONstring.Equals(""))
                m_Data = JObject.Parse(JSONstring);

            m_Provider = (HasRootNode("provider") ? new GameStateNode(m_Data["provider"]) : GameStateNode.Empty());
            m_Map = (HasRootNode("map") ? new GameStateNode(m_Data["map"]) : GameStateNode.Empty());
            m_Round = (HasRootNode("round") ? new GameStateNode(m_Data["round"]) : GameStateNode.Empty());
            m_Player = (HasRootNode("player") ? new GameStateNode(m_Data["player"]) : GameStateNode.Empty());
            m_Auth = (HasRootNode("auth") ? new GameStateNode(m_Data["auth"]) : GameStateNode.Empty());
            m_Added = (HasRootNode("added") ? new GameStateNode(m_Data["added"]) : GameStateNode.Empty());
            m_Previously = (HasRootNode("previously") ? new GameStateNode(m_Data["previously"]) : GameStateNode.Empty());
        }

        /// <summary>
        /// Determines if the specified node exists in this GameState object 
        /// </summary>
        /// <param name="rootnode"></param>
        /// <returns>Returns true if the specified node exists, false otherwise</returns>
        public bool HasRootNode(string rootnode)
        {
            return (m_Data != null && m_Data[rootnode] != null);
        }
    }

    /// <summary>
    /// A sub node of a GameState object
    /// </summary>
    public class GameStateNode
    {
        private JToken m_Data;

        private GameStateNode()
        {
            m_Data = JToken.Parse("{}");
        }

        /// <summary>
        /// Initializes a new GameStateNode using a JToken object
        /// </summary>
        /// <param name="node"></param>
        public GameStateNode(JToken node)
        {
            m_Data = node;
        }

        /// <summary>
        /// Get the value of a specific subnode of this GameStateNode
        /// </summary>
        /// <param name="node">The name of the subnode to get the value of</param>
        /// <returns>The string value of the specified subnode</returns>
        public string GetValue(string node)
        {
            if (m_Data[node] == null)
                return "";

            return m_Data[node].ToString();
        }

        /// <summary>
        /// Get a specific subnode as a new GameStateNode
        /// </summary>
        /// <param name="node">The name of the subnode</param>
        /// <returns>A new GameStateNode object containing the subnode</returns>
        public GameStateNode GetNode(string node)
        {
            if (m_Data[node] == null)
                return GameStateNode.Empty();

            return new GameStateNode(m_Data[node]);
        }

        /// <summary>
        /// An empty GameStateNode to substitute for a null value
        /// </summary>
        /// <returns></returns>
        public static GameStateNode Empty()
        {
            return new GameStateNode();
        }

        /// <summary>
        /// Get a specific subnode as a new GameStateNode
        /// </summary>
        /// <param name="node">The name of the subnode to get the value of</param>
        /// <returns>A new GameStateNode object containing the subnode</returns>
        public GameStateNode this[string node]
        {
            get { return GetNode(node); }
        }
    }

    /// <summary>
    /// A class that listens for HTTP POST requests and keeps track of previous game states
    /// </summary>
    public static class GSIListener
    {
        private const int MAX_GAMESTATES = 10;

        private static AutoResetEvent waitForConnection = new AutoResetEvent(false);
        private static List<GameState> GameStates = new List<GameState>();

        /// <summary>
        /// The most recently received GameState object
        /// </summary>
        public static GameState CurrentGameState
        {
            get
            {
                if (GameStates.Count > 0)
                    return GameStates[GameStates.Count - 1];
                else
                    return null;
            }
        }

        private static int m_Port;
        private static bool m_Running = false;
        private static HttpListener listener;

        /// <summary>
        /// Gets the port that is currently listening
        /// </summary>
        public static int Port { get { return m_Port; } }

        /// <summary>
        /// Gets a bool determining if the listening process is running
        /// </summary>
        public static bool Running { get { return m_Running; } }

        /// <summary>
        /// Occurs after a new GameState has been received
        /// </summary>
        public static event EventHandler NewGameState = delegate { };

        /// <summary>
        /// Starts listening for HTTP POST requests on the specified port<para />
        /// !!! Fails if the application is started without administrator privileges !!!
        /// </summary>
        /// <param name="port">The port to listen on</param>
        /// <returns>Returns true if the listener could be started, false otherwise</returns>
        public static bool Start(int port)
        {
            if (!m_Running && UacHelper.IsProcessElevated)
            {
                m_Port = port;
                listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                Thread listenerThread = new Thread(new ThreadStart(Run));
                m_Running = true;
                listenerThread.Start();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Stops listening for HTTP POST requests
        /// </summary>
        public static void Stop()
        {
            m_Running = false;
        }

        private static void Run()
        {
            try
            {
                listener.Start();
            }
            catch (HttpListenerException)
            {
                m_Running = false;
                return;
            }
            while (m_Running)
            {
                listener.BeginGetContext(ReceiveGameState, listener);
                waitForConnection.WaitOne();
                waitForConnection.Reset();
            }
            listener.Stop();
        }

        private static void ReceiveGameState(IAsyncResult result)
        {
            try
            {
                HttpListenerContext context = listener.EndGetContext(result);
                HttpListenerRequest request = context.Request;
                string JSON;

                waitForConnection.Set();

                using (Stream inputStream = request.InputStream)
                {
                    using (StreamReader sr = new StreamReader(inputStream))
                    {
                        JSON = sr.ReadToEnd();
                    }
                }
                using (HttpListenerResponse response = context.Response)
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.StatusDescription = "OK";
                    response.Close();
                }

                GameState gs = new GameState(JSON);
                GameStates.Add(gs);
                NewGameState(gs, EventArgs.Empty);

                while (GameStates.Count > MAX_GAMESTATES)
                    GameStates.RemoveAt(0);
            }
            catch (Exception)
            {
                
            }
        }
    }

    //stolen in it's entirety from http://stackoverflow.com/a/4497572 :O
    //this class is necessary since HttpListener.Start() will throw an exception if the application isn't started with admin privileges
    internal static class UacHelper
    {
        private const string uacRegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System";
        private const string uacRegistryValue = "EnableLUA";

        private static uint STANDARD_RIGHTS_READ = 0x00020000;
        private static uint TOKEN_QUERY = 0x0008;
        private static uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        public enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            TokenElevationType,
            TokenLinkedToken,
            TokenElevation,
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            MaxTokenInfoClass
        }

        public enum TOKEN_ELEVATION_TYPE
        {
            TokenElevationTypeDefault = 1,
            TokenElevationTypeFull,
            TokenElevationTypeLimited
        }

        public static bool IsUacEnabled
        {
            get
            {
                RegistryKey uacKey = Registry.LocalMachine.OpenSubKey(uacRegistryKey, false);
                bool result = uacKey.GetValue(uacRegistryValue).Equals(1);
                return result;
            }
        }

        public static bool IsProcessElevated
        {
            get
            {
                if (IsUacEnabled)
                {
                    IntPtr tokenHandle;
                    if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_READ, out tokenHandle))
                    {
                        throw new ApplicationException("Could not get process token.  Win32 Error Code: " + Marshal.GetLastWin32Error());
                    }

                    TOKEN_ELEVATION_TYPE elevationResult = TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault;

                    int elevationResultSize = Marshal.SizeOf((int)elevationResult);
                    uint returnedSize = 0;
                    IntPtr elevationTypePtr = Marshal.AllocHGlobal(elevationResultSize);

                    bool success = GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevationType, elevationTypePtr, (uint)elevationResultSize, out returnedSize);
                    if (success)
                    {
                        elevationResult = (TOKEN_ELEVATION_TYPE)Marshal.ReadInt32(elevationTypePtr);
                        bool isProcessAdmin = elevationResult == TOKEN_ELEVATION_TYPE.TokenElevationTypeFull;
                        return isProcessAdmin;
                    }
                    else
                    {
                        throw new ApplicationException("Unable to determine the current elevation.");
                    }
                }
                else
                {
                    WindowsIdentity identity = WindowsIdentity.GetCurrent();
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    bool result = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    return result;
                }
            }
        }
    }
}
