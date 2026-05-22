namespace Chronos.Orchestration.Utils;

/// <summary>
/// Encodes/decodes a double[] gene array into a portable Base64 string for storage and comparison.
/// </summary>
public static class SeedToolService
{
    /// <summary>Returns a portable DNA string (Base64) or "DEFAULT" if null/empty.</summary>
    public static string GetPortableDna(double[]? genes)
    {
        if (genes is null or { Length: 0 })
        {
            return "DEFAULT";
        }

        byte[] bytes = new byte[genes.Length * sizeof(double)];
        Buffer.BlockCopy(genes, 0, bytes, 0, bytes.Length);
        string base64 = Convert.ToBase64String(bytes).TrimEnd('=');
        return "chr_v1_" + base64.Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Decodes a portable DNA string back to a gene array, or null if invalid.</summary>
    public static double[]? DecodePortableDna(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (id == "DEFAULT")
        {
            return null;
        }

        if (!id.StartsWith("chr_v1_", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            string base64 = id[7..].Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }

            byte[] bytes = Convert.FromBase64String(base64);
            double[] genes = new double[bytes.Length / sizeof(double)];
            Buffer.BlockCopy(bytes, 0, genes, 0, bytes.Length);
            return genes;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
