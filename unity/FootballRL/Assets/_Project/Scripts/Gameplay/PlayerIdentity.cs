using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Stabilne ID zawodnika — nie indeks obiektu na scenie.</summary>
    public class PlayerIdentity : MonoBehaviour
    {
        [SerializeField] int playerId;
        [SerializeField] int teamId;

        public int PlayerId => playerId;
        public int TeamId => teamId;

        public void Configure(int id, int team)
        {
            playerId = id;
            teamId = team;
        }
    }
}
