public static class URL {
    public static string BaseUrl = "https://easiest-venally-paola.ngrok-free.dev";

    public static string AuthRegisterPath = "/auth/register";
    public static string AuthLoginPath = "/auth/login";
    public static string AuthValidatePath = "/auth/validate";

    public static string MatchJoinPath = "/match/join";
    public static string MatchStatusPath = "/match/status";
    public static string MatchCharacterSelectionPath = "/match/character-selection";
    public static string MatchCharacterSelectionCancelPath = "/match/character-selection/cancel";
    public static string MatchCharacterSelectionStatusPath = "/match/character-selection/status";

    public static string WorldRoomConfigPath = "/room";

    // Optional local fallback. Prefer the matchmaker's world_endpoint for real matches.
    public static string WorldServer = "127.0.0.1:30080";
}
