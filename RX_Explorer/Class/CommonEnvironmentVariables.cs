using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class CommonEnvironmentVariables
    {
        public static async Task<string> TranslateVariable(string Variable)
        {
            if (string.IsNullOrWhiteSpace(Variable))
            {
                return string.Empty;
            }
            else
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    return await Exclusive.Controller.GetVariablePathAsync(Variable.Trim('%')).ConfigureAwait(false);
                }
            }
        }

        public static bool CheckIfContainsVariable(string Path)
        {
            return Regex.IsMatch(Path, @"(?<=(%))[\s\S]+(?=(%))");
        }

        public static async Task<IEnumerable<string>> GetVariablePathSuggestionAsync(string PartialVariablePath)
        {
            if (string.IsNullOrWhiteSpace(PartialVariablePath))
            {
                return new List<string>(0);
            }
            else
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    IReadOnlyList<string> Result = await Exclusive.Controller.GetVariableSuggestionAsync(PartialVariablePath);
                    return Result.Select((Var) => $"%{Var}%");
                }
            }
        }

        public static async Task<string> ReplaceVariableAndGetActualPathAsync(string PathWithVariable)
        {
            if (string.IsNullOrWhiteSpace(PathWithVariable))
            {
                return string.Empty;
            }
            else
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    string TempString = PathWithVariable;

                    foreach (string Var in Regex.Matches(PathWithVariable, @"(?<=(%))[\s\S]+(?=(%))").Select((Item) => Item.Value).Distinct())
                    {
                        string ActualPath = await Exclusive.Controller.GetVariablePathAsync(Var).ConfigureAwait(false);

                        if (string.IsNullOrWhiteSpace(ActualPath))
                        {
                            throw new System.Exception("ActualPath which get from variable is empty");
                        }
                        else
                        {
                            TempString = TempString.Replace($"%{Var}%", ActualPath);
                        }
                    }

                    return TempString;
                }
            }
        }
    }
}
