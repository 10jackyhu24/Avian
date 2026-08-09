public enum CharacterSelectionMode {
    None,
    SinglePlayer,
    P2P,
    Match
}

public static class GameState {
    public static bool isPlaying = false;
    public static bool hasWorldSeed = false;
    public static int currentSeed = 0;
    public static string currentRoomId = string.Empty;
    public static string selectedCharacterId = string.Empty;
    public static CharacterSelectionMode characterSelectionMode = CharacterSelectionMode.None;

    public static bool hasPendingSinglePlayerWorld = false;
    public static int pendingSinglePlayerSeed = 0;
    public static bool hasPendingMatchWorld = false;
    public static string pendingMatchRoomId = string.Empty;
    public static int pendingMatchSeed = 0;
    public static string pendingMatchEndpoint = string.Empty;

    public static void SetWorld(string roomId, int seed) {
        currentRoomId = roomId;
        currentSeed = seed;
        hasWorldSeed = true;
        isPlaying = true;
        hasPendingSinglePlayerWorld = false;
        hasPendingMatchWorld = false;
        characterSelectionMode = CharacterSelectionMode.None;
    }

    public static void ResetWorld() {
        currentRoomId = string.Empty;
        currentSeed = 0;
        hasWorldSeed = false;
        isPlaying = false;
        selectedCharacterId = string.Empty;
        characterSelectionMode = CharacterSelectionMode.None;
        hasPendingSinglePlayerWorld = false;
        pendingSinglePlayerSeed = 0;
        hasPendingMatchWorld = false;
        pendingMatchRoomId = string.Empty;
        pendingMatchSeed = 0;
        pendingMatchEndpoint = string.Empty;
    }

    public static void SetSelectedCharacter(string characterId) {
        selectedCharacterId = characterId;
    }

    public static void SetCharacterSelectionMode(CharacterSelectionMode mode) {
        characterSelectionMode = mode;
    }

    public static void SetPendingSinglePlayerWorld(int seed) {
        pendingSinglePlayerSeed = seed;
        hasPendingSinglePlayerWorld = true;
        hasPendingMatchWorld = false;
        SetCharacterSelectionMode(CharacterSelectionMode.SinglePlayer);
    }

    public static void SetPendingMatchWorld(string roomId, int seed, string endpoint) {
        pendingMatchRoomId = roomId;
        pendingMatchSeed = seed;
        pendingMatchEndpoint = endpoint;
        hasPendingMatchWorld = true;
        hasPendingSinglePlayerWorld = false;
        SetCharacterSelectionMode(CharacterSelectionMode.Match);
    }
}
