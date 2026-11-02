using HomeGroceryApi.Application.Dtos;
using HomeGroceryApi.Application.Interfaces;
using HomeGroceryApi.Domain.Entidades;
using HomeGroceryApi.Domain.Enums;
using HomeGroceryApi.Domain.Interfaces;
using HomeGroceryApi.Domain.Servicos;

namespace HomeGroceryApi.Application.Servicos;

/// <summary>
/// Descobre em qual grupo o usuário está agora e garante que ele sempre tenha um. Quem sai ou é removido
/// de uma família volta ao próprio grupo pessoal; se não existir mais nenhum, um novo é criado (com as
/// categorias iniciais), então ninguém fica sem lugar para guardar suas listas.
/// </summary>
public sealed class ServicoDeGrupoAtivo
{
    private readonly IGrupoRepository _grupos;
    private readonly IRelogio _relogio;

    public ServicoDeGrupoAtivo(IGrupoRepository grupos, IRelogio relogio)
    {
        _grupos = grupos;
        _relogio = relogio;
    }

    /// <summary>Não grava: quem chama confirma com a unidade de trabalho.</summary>
    public async Task<Guid> ResolverAsync(ContaDoUsuario conta, CancellationToken ct)
    {
        var ativo = await _grupos.ObterGrupoAtivoDoUsuarioAsync(conta.Id, ct);
        if (ativo.HasValue) return ativo.Value;

        var vinculo = (await _grupos.ListarVinculosDoUsuarioAsync(conta.Id, ct)).OrderBy(v => v.EntrouEm).FirstOrDefault();
        if (vinculo is not null)
        {
            vinculo.Ativar();
            return vinculo.GrupoId;
        }

        var agora = _relogio.AgoraUtc;
        var grupo = Grupo.CriarPessoal(Guid.CreateVersion7(), conta.Nome, agora);
        var membro = MembroDoGrupo.Criar(grupo.Id, conta.Id, PapelNoGrupoEnum.Administrador, agora);
        await _grupos.AdicionarAsync(grupo, membro, CategoriasIniciais.CriarPara(grupo.Id, Guid.CreateVersion7), ct);
        return grupo.Id;
    }
}
