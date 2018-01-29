namespace DurableFunctionVideoProcessor
{
    static class ActivityNames
    {
        public const string ExtractThumbnail = "A_ExtractThumbnail";
        public const string PrependIntro = "A_PrependIntro";
        public const string PublishVideo = "A_PublishVideo";
        public const string RejectVideo = "A_RejectVideo";
        public const string Cleanup = "A_Cleanup";
        public const string GetTranscodeProfiles = "A_GetTranscodeProfiles";
        public const string TranscodeVideo = "A_TranscodeVideo";
        public const string SendApprovalRequestEmail = "A_SendApprovalRequestEmail";
        public const string PeriodicActivity = "A_PeriodicActivity";
    }
}