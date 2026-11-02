using HomeGroceryApi.Application.Excecoes;
using HomeGroceryApi.Domain.Excecoes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HomeGroceryApi.API.Infra;

/// <summary>
/// Ponto único de tratamento de erros: converte erros de negócio no status HTTP certo e
/// esconde qualquer detalhe técnico dos erros inesperados (que só vão para o log).
/// </summary>
public sealed class TratadorDeErros : IExceptionHandler
{
    private readonly ILogger<TratadorDeErros> _log;

    public TratadorDeErros(ILogger<TratadorDeErros> log)
    {
        _log = log;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken ct)
    {
        var (status, titulo) = excecao switch
        {
            ErroDeValidacao => (StatusCodes.Status400BadRequest, "Dados inválidos"),
            ErroDeAutenticacao => (StatusCodes.Status401Unauthorized, "Não autenticado"),
            ErroDeNaoEncontrado => (StatusCodes.Status404NotFound, "Não encontrado"),
            ErroDeAcessoNegado => (StatusCodes.Status403Forbidden, "Acesso negado"),
            ErroDeConflito => (StatusCodes.Status409Conflict, "Conflito"),
            ErroDeDominio => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (StatusCodes.Status409Conflict, "Conflito"),
            _ => (StatusCodes.Status500InternalServerError, "Erro inesperado"),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _log.LogError(excecao, "Erro não tratado em {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
        }

        var problema = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = excecao switch
            {
                ErroDeAplicacao or ErroDeDominio => excecao.Message,
                DbUpdateConcurrencyException => "Este registro foi alterado por outra pessoa. Atualize e tente de novo.",
                _ when status == StatusCodes.Status409Conflict => "Já existe um registro igual a este.",
                _ => "Algo deu errado. Tente novamente em instantes.",
            },
        };

        if (excecao is ErroDeValidacao validacao) problema.Extensions["erros"] = validacao.Detalhes;

        contexto.Response.StatusCode = status;
        await contexto.Response.WriteAsJsonAsync(problema, ct);
        return true;
    }
}
