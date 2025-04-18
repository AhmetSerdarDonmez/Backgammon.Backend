using Backgammon.Backend.Services;
using Backgammon.Backend.Hubs;

var builder = WebApplication.CreateBuilder(args);

// --- CORS Configuration ---
var AllowReactDevApp = "_allowReactDevApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: AllowReactDevApp,
                      policy =>
                      {
                          policy.SetIsOriginAllowed(origin =>
                          {
                              // Allow any origin that starts with "http://10." or "https://10."
                              return origin.StartsWith("http://10.") || origin.StartsWith("https://10.") || origin.StartsWith("http://localhost")|| origin.StartsWith("https://192.")|| origin.StartsWith("http://192.");
                          })
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); // Required for SignalR
                      });
});

// --- Add services to the container ---
builder.Services.AddSignalR();
builder.Services.AddSingleton<GameService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Listen on the local network IP
builder.WebHost.UseUrls("http://10.16.4.28:5369");

var app = builder.Build();

// --- Configure the HTTP request pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS policy globally
app.UseCors(AllowReactDevApp);

app.UseRouting();

// Map the SignalR Hub endpoint
app.MapHub<GameHub>("/gamehub");

// Example minimal API endpoint (optional)
app.MapGet("/", () => "Backgammon Server is running!");

app.Run();