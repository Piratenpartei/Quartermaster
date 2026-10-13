using System;
using System.Net.Http;

namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

public static class OpenSlidesErrorText {
    public const string Unexpected = "Unerwarteter Fehler, Details stehen im Serverprotokoll.";
    public const string Unreachable = "OpenSlides ist nicht erreichbar.";
    public const string Timeout = "Zeitüberschreitung bei der Anfrage an OpenSlides.";
    public const int MaxActionMessageLength = 500;

    /// <summary>A German text chosen only by exception type and <see cref="OpenSlidesException.Code"/>, never from a message that can hold a response body.</summary>
    public static string Fixed(Exception exception) {
        if (exception is HttpRequestException)
            return Unreachable;
        if (exception is TimeoutException || exception is OperationCanceledException)
            return Timeout;
        if (exception is not OpenSlidesException openSlides)
            return Unexpected;

        switch (openSlides.Code) {
            case "login_failed":
            case "unauthorized":
                return "Benutzername oder Passwort wurden abgelehnt.";
            case "insecure_url":
                return "Die OpenSlides-URL muss https verwenden.";
            case "timeout":
                return Timeout;
            case "http_error":
                return "OpenSlides ist nicht erreichbar oder hat die Anfrage abgelehnt.";
            case "action_failed":
                return "OpenSlides hat die Aktion abgelehnt.";
            default:
                return "OpenSlides hat unerwartet geantwortet.";
        }
    }

    /// <summary>Like <see cref="Fixed"/>, but the message OpenSlides gave for a rejected action passes through, capped.</summary>
    public static string ForAction(Exception exception) {
        if (!HasActionMessage(exception, out var message))
            return Fixed(exception);

        return message.Length > MaxActionMessageLength ? message[..MaxActionMessageLength] : message;
    }

    private static bool HasActionMessage(Exception exception, out string message) {
        message = "";
        if (exception is not OpenSlidesException openSlides)
            return false;
        if (openSlides.Code != "action_failed")
            return false;
        if (string.IsNullOrWhiteSpace(openSlides.Message))
            return false;

        message = openSlides.Message;
        return true;
    }
}
