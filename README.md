# CSGOSharp

C# Library for Counter-Strike: Global Offensive Game State Integration


https://developer.valvesoftware.com/wiki/Counter-Strike:_Global_Offensive_Game_State_Integration


There are some processed values such as round timers/bomb timers.
Always run the application with adminitrator rights, when debugging, run Visual Studio as administrator.


Get started: 

    - Add: http://pastebin.com/4MwDZ9Um to "Counter-Strike Global Offensive\csgo\cfg\gamestate_integration_test.cfg"
    - Add: using CSGSI;
    - Download Newtonsoft.Json from NuGet
    - For event handeler: 
            CSGOSharp.NewGameState += new CSGOSharp.NewGameStateHandler(onNewgameState);
            private void onNewgameState() { }
            - To start: CSGOSharp.Start();
            - To stop: CSGOSharp.Stop();


See http://pastebin.com/d6hsmV89 for all variables/objects in this library.


Credit to https://github.com/rakijah/CSGSI for GSIListener class.
