public static class URL {
    public static string BaseUrl = "https://8637-219-69-234-213.ngrok-free.app";

    public static string AuthRegisterPath = "/auth/register";
    public static string AuthLoginPath = "/auth/login";
    public static string AuthValidatePath = "/auth/validate";

    public static string MatchJoinPath = "/match/join";
    public static string MatchStatusPath = "/match/status";

    public static string WorldRoomConfigPath = "/room";

    // Optional local fallback. Prefer the matchmaker's world_endpoint for real matches.
    public static string WorldServer = "127.0.0.1:30080";
}
