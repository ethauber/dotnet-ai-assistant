namespace Core.Exceptions;

public sealed class PromptTemplateNotFoundException : Exception
{
    public PromptTemplateNotFoundException(string path)
        : base($"Prompt template was not found at '{path}'.")
    {
        Path = path;
    }

    public string Path { get; }
}
