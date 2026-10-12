namespace ILLiasu_Local_Mall.Infrastructure.Storage;

// No files are written until a durable store and decoded-content validator are approved.
public interface IProductImageUploadBoundary
{
    IReadOnlyList<string> Validate(IReadOnlyList<IFormFile> files, int? primary);
}
public sealed class DisabledProductImageUploadBoundary : IProductImageUploadBoundary
{
    public IReadOnlyList<string> Validate(IReadOnlyList<IFormFile> files, int? primary)
    {
        var errors=new List<string>();
        if(files.Count==0) { if(primary.HasValue) errors.Add("Select an image before choosing a primary image.");return errors; }
        if(files.Count>5) errors.Add("Select at most five images.");
        if(files.Any(x=>x.Length<=0 || x.Length>5*1024*1024)) errors.Add("Each image must be between one byte and 5 MB.");
        if(files.Any(x=>x.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))) errors.Add("Only JPEG, PNG and WebP candidates are allowed; file labels do not prove valid image content.");
        if(primary is null || primary<0 || primary>=files.Count) errors.Add("Choose one primary image.");
        errors.Add("Image saving is not enabled: durable storage and decoded-image validation are pending. Remove selected files to save this product without images.");
        return errors;
    }
}
