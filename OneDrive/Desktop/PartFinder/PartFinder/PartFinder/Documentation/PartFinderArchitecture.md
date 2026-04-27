# PartFinder Architecture Implementation Notes

## Reusable components and API shape

- `ShellLayout`
  - Purpose: global SaaS shell (sidebar + topbar + frame host)
  - Inputs: `ShellViewModel`
  - Output: hosted navigation pages through `INavigationService`
- `PageHeader`
  - Props: `Title`, `Subtitle`, `Actions`
  - Usage: standardized page title/action region with consistent spacing
- `KpiCard`
  - Props: `Label`, `Value`, `Delta`
  - Usage: dashboard and report summary blocks
- `FilterBuilderControl`
  - Purpose: advanced filtering entry point (field + condition + value pattern)
- `DynamicFormRenderer`
  - Props: `Fields` (`IReadOnlyList<TemplateFieldDefinition>`)
  - Purpose: render template-driven form editors from metadata
- `LoadingSkeleton`
  - Purpose: immediate visual feedback during data fetches

## Dynamic metadata model

- Template schema is defined by:
  - `PartTemplateDefinition` (template identity, version, publish state)
  - `TemplateFieldDefinition` (field key/label/type/validation/order/options)
- Data rows are represented by:
  - `PartRecord` with `Values: Dictionary<string, object?>`
- Grid columns are generated from template fields at runtime:
  - `GridColumnDefinition` + `PartsPage.BuildGridColumns()`

## Performance blueprint implemented

- Async-only loading path:
  - `PartsViewModel.InitializeAsync()`, `RefreshAsync()`, `LoadMoreAsync()`
- Request cancellation and coalescing:
  - `CancellationTokenSource` replaces in-flight requests on refresh/template switch
- Incremental paging:
  - `IPartsDataService.GetPageAsync(templateId, offset, pageSize)`
  - Default page size: 200 rows
- Render cost control:
  - DataGrid `AutoGenerateColumns=False`, explicit dynamic columns only
  - Lightweight cell binding from dictionary values
- Startup optimization:
  - only shell + initial route loaded at launch
  - page-specific ViewModels resolved on demand through DI

## Design tokens

- Colors in `Styles/Colors.xaml` with theme dictionaries (`Default`, `Dark`)
- Typography in `Styles/Typography.xaml` (title, section, body defaults)
- Spacing in `Styles/Spacing.xaml` (4px scale, page/card tokens)
- Common controls in `Styles/Controls.xaml` (cards, input rounding, button density)
