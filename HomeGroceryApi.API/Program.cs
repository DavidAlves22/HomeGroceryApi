using HomeGroceryApi.API.Infra;
using HomeGroceryApi.CrossCutting;
using HomeGroceryApi.CrossCutting.IoC;
using HomeGroceryApi.Infrastructure.Persistencia;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Hospedagens como o Render informam a porta pela variável PORT.
var porta = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(porta)) builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<TratadorDeErros>();
builder.Services.AddProblemDetails();

builder.Services.AddInfraestrutura(builder.Configuration);
builder.Services.AddCasosDeUso();
builder.Services.AddConfiguracoesIniciais(builder.Configuration);

var app = builder.Build();

// A hospedagem termina o HTTPS e repassa em HTTP; sem isto a API acharia que o cliente usa HTTP.
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });

// Container não roda `dotnet ef database update` à parte: o próprio processo aplica o que faltar ao subir.
using (var escopo = app.Services.CreateScope())
{
    escopo.ServiceProvider.GetRequiredService<HomeGroceryDbContext>().Database.Migrate();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(ConfiguracoesIniciais.PoliticaDeCors);
app.UseRateLimiter();

// A ordem importa: a autenticação precisa preencher a identidade antes da autorização conferi-la.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
