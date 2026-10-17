namespace MyGamingMap.API.Exceptions;

public class InvalidNpssoException()
    : Exception("Not a valid NPSSO.");

public class PSNAuthenticationFailed()
    : Exception("PSN Authentication failed. The NPSSO has likely expired. MyGamingMap has been notified.");

public class PlayerNotFoundException()
    : Exception("User not found.");

public class NoGamesFoundException()
    : Exception("No games found for this user.");

public class RateLimitExceededException()
    : Exception("Rate limit reached. Please wait before sending any more requests.");