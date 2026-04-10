 `README.md`

````markdown
# GF INI Editor

Editor desktop para arquivos `.ini` do Grand Fantasia, com foco em visualização, edição e salvamento direto dos dados do cliente.

## Funcionalidades
- Suporte a Múltiplos idiomas  `Português-BR, English, Spanish, Francês e Chinês`
- Edição de `S/C_.inis`
- Leitura de tradução via `T_.ini` 
- Busca de itens por ID ou nome
- Visualização de ícones `.dds`
- Edição de atributos e campos gerais
- Edição de flags e classes com interface visual
- Clonagem de itens
- Salvamento direto nos arquivos `.ini`
- Suporte a campos de reputação/fama via dicionário no `RestrictAlign`
- Visualização clara em Condições e Tipagens via legendas ituitivas.

## Módulos atuais

- **Geral**
- **Itens**
- **ItemMall**
- **Mission**
- **Store**
- **Elf Combine**
- **Drop Item**
- **Enchant**
- **Collection**
- **Npc**
- **Monster**
- **Scene**

## Tecnologias usadas

- C#
- WPF
- .NET 8 Windows
- Pfim (leitura de imagens `.dds`)

## Estrutura esperada do cliente

O editor espera uma estrutura semelhante a esta:

```text
data/
 ├─ db/
 │   ├─ S_Item.ini
 │   ├─ C_Item.ini
 │   ├─ S_ItemMall.ini
 │   ├─ C_ItemMall.ini
 │
 └─ translate/
     ├─ T_Item.ini
     ├─ T_ItemMall.ini

UI/
 └─ itemicon/
     └─ *.dds
````

## Como usar


Obs:precisa manter os .Inis S_ e C_ nas pasta DB do Seu cliente , o salvamente será feito em ambos os arquivos .


1. Abra o programa
2. Selecione o caminho do cliente
3. Escolha o módulo desejado no menu lateral
4. Pesquise o item pelo ID ou nome
5. Edite os campos desejados
6. Clique em **Salvar**

## Publicação

A release inicial foi publicada em modo:

* **Release**
* **Framework-dependent**
* **win-x64**
* sem `ReadyToRun`
* sem `Single File`

## Observações

* O projeto usa codificações específicas como `950` e `1252` para leitura e escrita dos arquivos.
* Algumas descrições em arquivos `T_*.ini` podem conter múltiplas linhas.
* É recomendado sempre manter backup dos arquivos antes de editar.

## Status do projeto

Versão inicial em desenvolvimento ativo.

## Roadmap

* Melhorias visuais na interface
* Novos módulos para outros arquivos `.ini`
* Melhor tratamento de traduções multilinha
* Mais validações de campos
* Organização e refino dos layouts

## Autor

Desenvolvido por Alex R. (Budda)

