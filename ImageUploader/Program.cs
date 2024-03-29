using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", async context =>
{
    context.Response.Headers["Content-Type"] = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.UseStaticFiles();

string uploadDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads"); //will save images here
if (!Directory.Exists(uploadDirectory))
{
    Directory.CreateDirectory(uploadDirectory);
}


// Endpoint for uploading an image
app.MapPost("/upload", async (HttpContext context, ILogger<Program> logger) =>
{

    var form = await context.Request.ReadFormAsync(); //read info from form
    var title = form["imgtitle"]; //get image title
    var file = form.Files["imgToUpload"]; //get file path
    
    // Validate inputs
    if (string.IsNullOrWhiteSpace(title) || file == null)
    {
        context.Response.StatusCode = 400; // Bad Request
        return;
    }

    // Check file extension
    var fileExtension = Path.GetExtension(file.FileName).ToLower();
    if (fileExtension != ".jpeg" && fileExtension != ".jpg" && fileExtension != ".png" && fileExtension != ".gif")
    {
        context.Response.StatusCode = 400; // Bad Request
        return;
    }

    // Generate unique ID for the image
    var imageId = Guid.NewGuid().ToString();

    // Save the uploaded image
    var filePath = Path.Combine(uploadDirectory, imageId + fileExtension); //path where the image will be saved /uploads/imgid.jpg
   
    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    // Store image information in JSON format
    var imageInfo = new ImageInfo { Id = imageId, Title = title, Imgpath = filePath, FileExtension = fileExtension };
   
    var jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images.json");
    List<ImageInfo> images;
    if (File.Exists(jsonFilePath))
    {
        var json = await File.ReadAllTextAsync(jsonFilePath);
        images = JsonSerializer.Deserialize<List<ImageInfo>>(json);
    }
    else
    {
        images = new List<ImageInfo>();
    }
    images.Add(imageInfo);
    var updatedJson = JsonSerializer.Serialize(images);
    await File.WriteAllTextAsync(jsonFilePath, updatedJson);
 
    // Redirect to the page with unique ID
   
    context.Response.Redirect($"/picture/{imageId}");

});

//endpoint for displaying the image
app.MapGet("/img/{id}", async (HttpContext context) =>
{
    var id = context.Request.RouteValues["id"].ToString();
    var jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images.json");
    if (!File.Exists(jsonFilePath))
    {
        context.Response.StatusCode = 404; // Not Found
        return;
    }

    var json = await File.ReadAllTextAsync(jsonFilePath);
    var images = JsonSerializer.Deserialize<List<ImageInfo>>(json);
    var imageInfo = images.FirstOrDefault(img => img.Id == id);
    if (imageInfo == null)
    {
        context.Response.StatusCode = 404; // Not Found
        return;
    }

    var imagePath = imageInfo.Imgpath;
    var contentType = imageInfo.FileExtension switch
    {
        ".jpeg" => "image/jpeg",
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        _ => "application/octet-stream" // Default to binary data
    };

    if (!File.Exists(imagePath))
    {
        context.Response.StatusCode = 404; // Not Found
        return;
    }

    context.Response.ContentType = contentType;
    await using var stream = File.OpenRead(imagePath);
    await stream.CopyToAsync(context.Response.Body);
});
 //endpoint for displaying image details
app.MapGet("/picture/{imageId}", async (HttpContext context) =>
{
    var imageId = context.Request.RouteValues["imageId"].ToString();
    var jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images.json");
    if (!File.Exists(jsonFilePath))
    {
        return Results.NotFound();
    }
    
    var json = await File.ReadAllTextAsync(jsonFilePath);
    var images = JsonSerializer.Deserialize<List<ImageInfo>>(json);
    var imageInfo = images.FirstOrDefault(img => img.Id == imageId);
    if (imageInfo == null)
    {
        
        return Results.NotFound();
    }
    var html = $@"
            <html>
         <head>
         
             <style>
                body {{
                    font-family: Arial, sans-serif;
                    background-color: #D3D3D3;
                    margin: 0;
                    padding: 0;
                }}
                h1, h2 {{
                    margin-top : 3vh;
                    color: #0096c7;
                    text-align: center;
                }}
                img {{
                    display: block;
                    margin: 0 auto;
                    max-width: 100%;
                    height: auto;
                }}
               
            </style>
           
        </head>
                <body>
            <h1>Image Details</h1>
            <h2>Title: {imageInfo.Title}</h2>
    <img src='/img/{imageId}' alt='{imageInfo.Title}' width='400'>
        </body>
            </html>";
   
    return Results.Content(html, "text/html");
   
});
app.Run();
public class ImageInfo
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Imgpath { get; set; }
    public string? FileExtension { get; set; }

}
