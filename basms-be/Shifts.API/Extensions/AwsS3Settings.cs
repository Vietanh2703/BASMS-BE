namespace Shifts.API.Extensions;

public class AwsS3Settings
{
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "ap-southeast-2";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string FolderPrefix { get; set; } = "shifts/evidence"; // Folder cho chứng từ nghỉ việc
}
