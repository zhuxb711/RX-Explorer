using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class EnvironmentVariables
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
            try
            {
                if (!string.IsNullOrWhiteSpace(PartialVariablePath))
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await Exclusive.Controller.GetVariableSuggestionAsync(PartialVariablePath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetVariablePathSuggestionAsync)}");
            }

            return new List<VariableDataPackage>(0);
        }

        public static async Task<string> ReplaceVariableWithActualPathAsync(string Input)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Input))
                {
                    string Variable = GetVariableInPath(Input);

                    if (string.IsNullOrEmpty(Variable))
                    {
                        return Input;
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ReplaceVariableWithActualPathAsync)}");
            }

            return string.Empty;
        }
    }
}
