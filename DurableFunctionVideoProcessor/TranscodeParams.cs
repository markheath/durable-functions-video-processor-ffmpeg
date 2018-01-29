namespace DurableFunctionVideoProcessor
{
    public class TranscodeParams
    {
        public string InputFile { get; set; }
        public string OutputExtension { get; set; }
        public string FfmpegParams { get; set; }
    }
}