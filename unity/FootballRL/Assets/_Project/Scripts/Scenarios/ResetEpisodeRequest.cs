using System;

namespace FootballRL.Scenarios
{
    public struct ResetEpisodeRequest
    {
        public int Seed;
        public ScenarioDefinition Scenario;
        public bool UseExactState;
        public ExactEpisodeState ExactState;

        public static ResetEpisodeRequest ForScenario(ScenarioDefinition scenario, int seed)
        {
            return new ResetEpisodeRequest
            {
                Seed = seed,
                Scenario = scenario,
                UseExactState = false
            };
        }

        public static ResetEpisodeRequest WithExactState(ExactEpisodeState state, ScenarioDefinition scenario)
        {
            return new ResetEpisodeRequest
            {
                Scenario = scenario,
                UseExactState = true,
                ExactState = state
            };
        }
    }
}
