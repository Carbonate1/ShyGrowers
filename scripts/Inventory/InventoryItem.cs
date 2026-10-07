using Godot;

public partial class InventoryItem : Resource
{
    public string name;
    public Texture texture;
    public float currentSellingPrice;
    private float sellingPriceAtHarvest;

    public InventoryItem(Plant plant)
    {
        name = plant.PlantTypeName;
        texture = plant.HarvestableTexture;
        sellingPriceAtHarvest = plant.GetSaleValue;
        currentSellingPrice = sellingPriceAtHarvest;
    }
}