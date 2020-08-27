using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class CommonEnvironmentVariables
    {
        public static async Task<string> TranslateVariable(string Variable)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Variable))
                {
                    return string.Empty;
                }
                else
                {
                    return await FullTrustExcutorController.Current.GetVariablePath(Variable.Trim('%')).ConfigureAwait(false);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async Task<string> ReplaceVariableAndGetActualPath(string PathWithVariable)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(PathWithVariable))
                {
                    return string.Empty;
                }
                else
                {
                    string TempString = PathWithVariable;

                    foreach (string Var in Regex.Matches(PathWithVariable, @"(?<=(%))[\s\S]+(?=(%))").Select((Item) => Item.Value).Distinct())
                    {
                        TempString = TempString.Replace($"%{Var}%", await FullTrustExcutorController.Current.GetVariablePath(Var).ConfigureAwait(false));
                    }

                    return TempString;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
