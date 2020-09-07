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
                return await FullTrustExcutorController.Current.GetVariablePath(Variable.Trim('%')).ConfigureAwait(false);
            }
        }

        public static async Task<string> ReplaceVariableAndGetActualPath(string PathWithVariable)
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
                    string ActualPath = await FullTrustExcutorController.Current.GetVariablePath(Var).ConfigureAwait(false);

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
