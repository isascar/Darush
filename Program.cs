global using MyJob.Data;
global using Microsoft.EntityFrameworkCore;

global using System.Security.Cryptography;
global using System.Text;
global using MyJob.DTOs;
global using AutoMapper;
global using Microsoft.AspNetCore.Mvc;
global using MyJob.Interfaces;
global using MyJob.Entities;
global using System.Security.Claims;
global using MyJob.Extensions;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddApplicationService(builder.Configuration);
builder.Services.AddIdentityService(builder.Configuration);


var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseCors(builder => builder.AllowAnyHeader().AllowAnyMethod().WithOrigins("http://localhost:4200", "https://localhost:4200"));

app.UseAuthorization();

app.MapControllers();

//--------------------------------------
using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var context = services.GetRequiredService<DataContext>();
    await context.Database.MigrateAsync();
    await Seed.SeedUsers(context);
}
catch (Exception ex)
{
    var logger = services.GetService<Logger<Program>>();
    logger.LogError(ex, "an error occurred during migration");
}
//--------------------------------------


app.Run();
