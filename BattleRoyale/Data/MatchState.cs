using System;
using System.Collections.Generic;

namespace BattleRoyale
{
    public enum MatchPhase
    {
        Lobby,
        WaitingForPlayers,
        Active,
        Ended
    }

    public class MatchState
    {
        public string MatchId;
        public int Seed;
        public MatchPhase Phase;
        public List<string> AlivePlayers = new List<string>();
        public int InitialPlayerCount;
        public DateTime StartTime;
        public string WinnerName;
    }
}
