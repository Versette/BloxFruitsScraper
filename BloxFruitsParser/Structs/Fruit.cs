namespace BloxFruitsScraper.Structs;

public struct Fruit
{
    public byte[] Image { get; }

    //public string Image { get; }
    public string Name { get; }
    public string Price { get; }

    public Fruit(string name, string price, byte[] image)
    {
        Name = name;
        Price = price;
        Image = image;
    }
}