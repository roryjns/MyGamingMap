namespace MyGamingMap.API.Services;

public static class StringSimilarity
{
    public static double JaroWinkler(string s1, string s2)
    {
        if (s1 == s2)
            return 1.0;

        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0.0;

        int len1 = s1.Length;
        int len2 = s2.Length;

        int matchDistance = Math.Max(len1, len2) / 2 - 1;

        var s1Matches = new bool[len1];
        var s2Matches = new bool[len2];

        int matches = 0;

        // Find matching characters
        for (int i = 0; i < len1; i++)
        {
            int start = Math.Max(0, i - matchDistance);
            int end = Math.Min(i + matchDistance + 1, len2);

            for (int j = start; j < end; j++)
            {
                if (s2Matches[j])
                    continue;

                if (s1[i] != s2[j])
                    continue;

                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0)
            return 0.0;

        // Count transpositions
        int k = 0;
        int transpositions = 0;

        for (int i = 0; i < len1; i++)
        {
            if (!s1Matches[i])
                continue;

            while (!s2Matches[k])
                k++;

            if (s1[i] != s2[k])
                transpositions++;

            k++;
        }

        double m = matches;

        double jaro =
            (m / len1 +
             m / len2 +
             (m - transpositions / 2.0) / m) / 3.0;

        // Winkler prefix bonus
        int prefix = 0;
        int prefixLimit = Math.Min(4, Math.Min(len1, len2));

        while (prefix < prefixLimit && s1[prefix] == s2[prefix])
            prefix++;

        return jaro + prefix * 0.1 * (1.0 - jaro);
    }
}