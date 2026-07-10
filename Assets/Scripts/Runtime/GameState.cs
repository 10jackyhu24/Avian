public static class GameState {
    public static bool isPlaying = false;
    public static bool hasWorldSeed = false;
    public static int currentSeed = 0;
    public static string currentRoomId = string.Empty;

    public static void SetWorld(string roomId, int seed) {
        currentRoomId = roomId;
        currentSeed = seed;
        hasWorldSeed = true;
        isPlaying = true;
    }
}
