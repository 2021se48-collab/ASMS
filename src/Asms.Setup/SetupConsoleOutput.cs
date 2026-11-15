namespace Asms.Setup;

public static class SetupConsoleOutput
{
    public static void WriteResult(
        TextWriter output,
        string schoolCode,
        string ownerUserName,
        SetupResult result)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Succeeded)
        {
            output.WriteLine($"School {schoolCode} created. Owner: {ownerUserName}.");
            return;
        }

        output.WriteLine(result.ErrorCode ?? "SETUP_FAILED");
    }
}
