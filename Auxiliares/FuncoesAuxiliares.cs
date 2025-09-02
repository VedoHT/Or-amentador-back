using Orcei.Models.Conexao;
using System.Text.RegularExpressions;
using System.Text;

namespace Orcei.Auxiliares
{
    public static class FuncoesAuxiliares
    {
        public static void Validar(bool condicao, string mensagem, int codigoErro)
        {
            if (condicao)
                throw new HaveException(mensagem, codigoErro);
        }

        public static int GerarCodigo()
        {
            return System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999);
        }

        public static int ToInt(this string stringAux)
        {
            int number;
            int.TryParse(stringAux, out number);
            return number;
        }

        public static long ToLong(this string stringAux)
        {
            long number;
            long.TryParse(stringAux, out number);
            return number;
        }

        public static float ToFloat(this string stringAux)
        {
            float number;
            float.TryParse(stringAux, out number);
            return number;
        }

        public static DateTime ToDateTime(this string stringAux)
        {
            DateTime date;
            DateTime.TryParse(stringAux, out date);
            return date;
        }

        public static DateTime ToDate(this string stringAux)
        {
            DateTime date;
            DateTime.TryParse(stringAux, out date);
            return date.Date;
        }

        public static char ToChar(this string stringAux)
        {
            char charString;
            char.TryParse(stringAux, out charString);
            return charString;
        }

        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        public static string ToSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Guid.NewGuid().ToString("n")[..8];

            string normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            string s = sb.ToString().Normalize(NormalizationForm.FormC);
            s = Regex.Replace(s, @"[^a-z0-9]+", "-").Trim('-');
            if (string.IsNullOrEmpty(s)) s = "item";
            return $"{s}-{Guid.NewGuid().ToString("n")[..6]}";
        }
    }
}
