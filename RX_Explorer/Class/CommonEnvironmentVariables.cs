using ShareClassLibrary;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class CommonEnvironmentVariables
    {
        public static bool CheckIfContainsVariable(string Path)
        {
            return Regex.IsMatch(Path, @"(?<=(%))[\s\S]+(?=(%))");
        }

        public static string GetVariableInPath(string Path)
        {
            string Variable = Regex.Match(Path, @"(?<=(%))[\s\S]+(?=(%))")?.Value;

            if (string.IsNullOrEmpty(Variable))
            {
                return string.Empty;
            }
            else
            {
                return $"%{Variable}%";
            }
        }

        public static async Task<IEnumerable<VariableDataPackage>> GetVariablePathSuggestionAsync(string PartialVariablePath)
        {
            if (string.IsNullOrWhiteSpace(PartialVariablePath))
            {
                return new List<VariableDataPackage>(0);
            }
            else
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    return await Exclusive.Controller.GetVariableSuggestionAsync(PartialVariablePath);
                }
            }
        }

        public static async Task<string> ReplaceVariableWithActualPathAsync(string Input)
        {
            if (string.IsNullOrWhiteSpace(Input))
            {
                return string.Empty;
            }
            else
            {
                string Variable = GetVariableInPath(Input);

                if (string.IsNullOrEmpty(Variable))
                {
                    return Input;
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        string ActualPath = await Exclusive.Controller.GetVariablePathAsync(Variable.Trim('%')).ConfigureAwait(false);

                        if (string.IsNullOrWhiteSpace(ActualPath))
                        {
                            return Input;
                        }
                        else
                        {
                            return Input.Replace(Variable, ActualPath);
                        }
                    }
                }
            }
        }
    }
}
