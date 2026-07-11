public static class URL {
    public static string BaseUrl = "http://localhost:8000";

    public static string AuthRegisterPath = "/auth/register";
    public static string AuthLoginPath = "/auth/login";
    public static string AuthValidatePath = "/auth/validate";

    public static string MatchJoinPath = "/match/join";
    public static string MatchStatusPath = "/match/status";

    public static string WorldRoomConfigPath = "/room";

    // Step 3 MVP: use one fixed Unity dedicated server while we prove client -> server works.
    // Later, this can be replaced by the matchmaker's world_endpoint per room.
    public static string WorldServer = "127.0.0.1:30080";
}
