using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;
using BloxFruitsScraper.Structs;

namespace BloxFruitsScraper;

public class Program
{
    private static async Task DrawFruits(IDictionary<string, List<Fruit>> fruitCategories)
    {
        Console.Clear();

        var renderables = new List<IRenderable>();
        for (var i = 0; i < fruitCategories.Count; i++)
        {
            var fruits = fruitCategories.ElementAt(i);

            if (fruits.Value.Count == 0)
                break;

            var table = new Table { Title = new TableTitle(fruits.Key), ShowRowSeparators = true };

            table.AddColumns(
                new TableColumn(new Markup("Fruit")).Centered(),
                new TableColumn(new Markup("Price")).Centered(),
                new TableColumn(new Markup("Preview")).Centered());
            foreach (var fruit in fruits.Value)
                table.AddRow(new Markup(fruit.Name), new Markup(fruit.Price),
                    fruit.Image.Length > 0 ? new CanvasImage(fruit.Image) { MaxWidth = 8 } : new Markup("Null"));

            renderables.Add(table);
        }

        AnsiConsole.Write(new Columns(renderables));
    }

    private static async Task Main()
    {
        using (var gamersbergScraper = new GamersbergScraper())
        {
            var fruitCategories = await gamersbergScraper.GetFruits();

            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = typeof(Program).Namespace!;
            Console.CursorVisible = false;

            if (fruitCategories == null || fruitCategories.Keys.Count == 0)
                return;

            await DrawFruits(fruitCategories);
        }

        Console.ReadLine();
    }
}