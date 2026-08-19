using Swashbuckle.AspNetCore.SwaggerUI;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// 1. ADD SERVICES (Dependency Injection)
// This tells the app, "Hey, I have controllers in this project.
// Please find them and make them ready to use."
// =========================================================
builder.Services.AddControllers();

// =========================================================
// 1b. NAMED HTTP CLIENT FOR DARAJA
// Beginners often write "new HttpClient()" inside a controller.
// That is a common .NET bug: it leaks TCP sockets under load
// ("socket exhaustion"). The fix is to let the framework manage
// one shared, pooled HttpClient for us via IHttpClientFactory.
// We just ask for it by name ("Daraja") wherever we need it.
// =========================================================
builder.Services.AddHttpClient("Daraja");

// =========================================================
// 2. SETUP SWAGGER (The Testing UI)
// Swagger reads our code and automatically generates a beautiful
// web page where we can click buttons to test our endpoints.
// =========================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// =========================================================
// 3. THE MIDDLEWARE PIPELINE
// This defines how HTTP requests flow through our app.
// =========================================================
if (app.Environment.IsDevelopment())
{
    // We only show the Swagger UI when we are coding locally
    // (i.e. ASPNETCORE_ENVIRONMENT = "Development").
    // We hide this in Production so hackers can't see all our endpoints!
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Automatically redirects any insecure HTTP requests to secure HTTPS
app.UseHttpsRedirection();

// Checks if the user has permission to access certain routes
app.UseAuthorization();

// =========================================================
// 4. ROUTING & EXECUTION
// =========================================================
// This command scans our app for [Route] attributes
// (like the one on our MpesaController) and wires them up to the web.
app.MapControllers();

// Finally, start the server and listen for incoming requests!
app.Run();