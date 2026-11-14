using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Categories;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;

namespace AdegaSystem.Tests.Services;

public class CategoryServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private CategoryService CreateService() =>
        new(_database.CreateContext(), new CreateCategoryDtoValidator(), new UpdateCategoryDtoValidator());

    [Fact]
    public async Task Create_SavesTrimmedNameAsActive()
    {
        var created = await CreateService().CreateAsync(new CreateCategoryDto("  Licor  ", "Licores em geral"));

        Assert.Equal("Licor", created.Name);
        Assert.True(created.Active);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task Create_WithDuplicateNameIgnoringCase_Throws()
    {
        // "Cerveja" já existe nas categorias iniciais (seed).
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(new CreateCategoryDto("cerveja", null)));

        Assert.Equal("Já existe uma categoria com esse nome.", exception.Message);
    }

    [Fact]
    public async Task Create_WithEmptyName_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().CreateAsync(new CreateCategoryDto("", null)));
    }

    [Fact]
    public async Task Create_WithBlankDescription_StoresNull()
    {
        // Descrição só com espaços conta como "sem descrição" (ver Text.EmptyToNull).
        var created = await CreateService().CreateAsync(new CreateCategoryDto("Licor", "   "));

        Assert.Null(created.Description);
    }

    [Fact]
    public async Task Update_WithDuplicateNameIgnoringCase_Throws()
    {
        var licor = await CreateService().CreateAsync(new CreateCategoryDto("Licor", null));

        // Renomear "Licor" para "VINHO" colide com a categoria "Vinho" do seed.
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().UpdateAsync(licor.Id, new UpdateCategoryDto("VINHO", null, true)));

        Assert.Equal("Já existe uma categoria com esse nome.", exception.Message);
        Assert.Equal("Licor", (await CreateService().GetByIdAsync(licor.Id)).Name);
    }

    [Fact]
    public async Task GetById_WhenMissing_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetByIdAsync(9999));
    }

    [Fact]
    public async Task Deactivate_WithActiveProducts_Throws()
    {
        var category = await CreateService().CreateAsync(new CreateCategoryDto("Licor", null));
        await using (var context = _database.CreateContext())
        {
            context.Products.Add(new Product { Name = "Licor 43", CategoryId = category.Id, SalePrice = 150 });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<BusinessException>(() => CreateService().DeactivateAsync(category.Id));
    }

    [Fact]
    public async Task Deactivate_WithoutActiveProducts_KeepsRecordAsInactive()
    {
        var category = await CreateService().CreateAsync(new CreateCategoryDto("Licor", null));

        await CreateService().DeactivateAsync(category.Id);

        // Soft delete: o registro continua no banco, só fica inativo.
        var reloaded = await CreateService().GetByIdAsync(category.Id);
        Assert.False(reloaded.Active);
    }

    public void Dispose() => _database.Dispose();
}
