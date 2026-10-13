using System;
using System.Linq;

namespace DacFXToolLib.Pipelines
{
    public class PipelineEnvironment
    {
        private static readonly string[] StagingOrProductionMarkers = { "prod", "stag", "stg", "live" };

        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a plan stage (deployment script and report)
        /// is generated and must be reviewed before deploying to this environment.
        /// </summary>
        public bool PlanBeforeDeploy { get; set; }

        public static PipelineEnvironment Create(string name)
        {
            return new PipelineEnvironment
            {
                Name = name,
                PlanBeforeDeploy = IsStagingOrProduction(name),
            };
        }

        public static bool IsStagingOrProduction(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return StagingOrProductionMarkers.Any(m => name.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
