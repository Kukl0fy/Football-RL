using System;
using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Bramka: linia w lokalnym układzie areny (Y do góry, boisko ±Z).</summary>
    [Serializable]
    public struct GoalDefinition
    {
        [Tooltip("Drużyna broniąca tej bramki.")]
        public int defendingTeamId;

        [Tooltip("Płaszczyzna linii bramkowej (Z). +10 = Goal_PosZ, −10 = Goal_NegZ.")]
        public float goalLineZ;

        [Tooltip("+1: gol gdy piłka przechodzi w rosnącym Z przez linię. −1: malejący Z.")]
        public float enterDirectionZ;
    }
}
