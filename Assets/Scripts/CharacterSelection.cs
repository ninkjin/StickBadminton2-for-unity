using UnityEngine;

public static class CharacterSelection
{
    public static Sprite LeftSprite;
    public static Sprite RightSprite;
    public static bool TwoPlayerMode = false;
    public static bool SkipIntro = false;

    public static void Clear()
    {
        LeftSprite = null;
        RightSprite = null;
        TwoPlayerMode = false;
        SkipIntro = false;
    }
}
