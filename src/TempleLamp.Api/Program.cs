using TempleLamp.Api.Extensions;
using TempleLamp.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ===== 加入服務 =====

// Controllers
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "點燈系統 API",
        Version = "v1",
        Description = "寺廟點燈管理系統 Web API"
    });

    // 加入 X-Workstation-Id Header 說明
    options.AddSecurityDefinition("WorkstationId", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "X-Workstation-Id",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "工作站識別碼（必填）"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "WorkstationId"
                }
            },
            Array.Empty<string>()
        }
    });

    // 載入 XML 註解（若存在）
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// 註冊 Repository 與 Service
builder.Services.AddRepositories();
builder.Services.AddServices();

// CORS（供本機 WPF 呼叫）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ===== 設定 Middleware Pipeline =====

// 開發環境啟用 Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "點燈系統 API v1");
        options.RoutePrefix = "swagger";
    });
}

// 全域例外處理（最外層）
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 工作站識別
app.UseMiddleware<WorkstationMiddleware>();

// CORS
app.UseCors();

// 路由
app.MapControllers();

// 首頁重導至 Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
