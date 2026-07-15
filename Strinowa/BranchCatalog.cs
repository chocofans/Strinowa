namespace StrinowaWPF;

internal static class BranchCatalog
{
    static readonly string[] ChinaGames =
    [
        "Game_Release", "Game_KOL", "Game_TYF", "Game_Test", "Game_Dev", "Game_Dev2", "Game_QQ", "Game_Expr"
    ];

    static readonly string[] ChinaLaunchers =
    [
        "Launcher_KOL", "Launcher_Release", "Launcher_Test", "Launcher_Dev", "Launcher_Dev2", "Launcher_TYF",
        "Launcher_CCZH", "Launcher_DGLX", "Launcher_HYKB", "Launcher_KOOK", "Launcher_LXWG", "Launcher_MTWG",
        "Launcher_PGGG", "Launcher_QQHY", "Launcher_QQWB", "Launcher_SWWG", "Launcher_TXSP", "Launcher_WFWG",
        "Launcher_WXPC", "Launcher_XGGG", "Launcher_YGX", "Launcher_YMXK", "Launcher_YQMT", "Launcher_Login",
        "Launcher_BB", "Launcher_QQ", "Launcher_SW", "Launcher_Expr", "Launcher_UU", "Launcher_FY", "Launcher_JKW",
        "Launcher_NGA", "Launcher_YYS", "Launcher_YGXWG", "Launcher_Xiaoheihe", "Launcher_DouYu", "Launcher_DouYin",
        "Launcher_KuaiShou", "Launcher_WeGame", "Launcher_IDSRelease"
    ];

    static readonly string[] OverseasGames =
    [
        "Game_Release", "Game_Preview", "Game_2024EWC", "Game_KOL", "Game_CE", "Game_PreTest",
        "Game_TestServer", "Game_Test", "Game_TGS", "Game_TW", "Game_USK", "Game_VNXD"
    ];

    static readonly string[] OverseasLaunchers =
    [
        "Launcher_Release", "Launcher_Preview", "Launcher_2024EWC", "Launcher_KOL", "Launcher_CE",
        "Launcher_PreTest", "Launcher_Strinova_Test", "Launcher_Strinova_TestL", "Launcher_Steam",
        "Launcher_SteamDemo", "Launcher_TestServer", "Launcher_Test", "Launcher_TW", "Launcher_TGS", "Launcher_USK",
        "Launcher_2_0_Overseas", "Launcher_Epic", "Launcher_FortuneStar", "Launcher_MiniRelease",
        "Launcher_PICA", "Launcher_SteamAlpha", "Launcher_SteamAPV", "Launcher_Stove", "Launcher_VNXD"
    ];

    static readonly string[] PcGames = ["Game_IDSTest"];
    static readonly string[] PcLaunchers = ["Launcher_IDSTest", "Launcher_IDSPreTest", "Launcher_IDSLX"];

    public static IReadOnlyList<string> Get(string source, string mode)
    {
        var launcher = mode.Equals("Launcher", StringComparison.OrdinalIgnoreCase);
        if (source.Equals("OS", StringComparison.OrdinalIgnoreCase))
            return launcher ? OverseasLaunchers : OverseasGames;
        var china = launcher ? ChinaLaunchers : ChinaGames;
        if (!source.Equals("PC", StringComparison.OrdinalIgnoreCase)) return china;
        return china.Concat(launcher ? PcLaunchers : PcGames).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string ShortName(string branch)
    {
        var index = branch.IndexOf('_');
        return index >= 0 && index + 1 < branch.Length ? branch[(index + 1)..] : branch;
    }
}
