namespace DurableFunctionVideoProcessor
{
    static class OrchestratorNames
    {
        public const string ProcessVideo = "O_ProcessVideo";
        public const string Transcode = "O_Transcode";
        public const string GetApprovalResult = "O_GetApprovalResult";
        public const string PeriodicTask = "O_PeriodicTask";
    }
}