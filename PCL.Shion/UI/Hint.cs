namespace PCL.Shion.UI;

public static class Hint
{
    public static void Show(string text, HintType type = HintType.Info) => HintService.Hint(text, type);
}
