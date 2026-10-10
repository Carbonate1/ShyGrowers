using Godot;

public partial class InventoryItem : Resource
{
    public string name;
    public Texture2D texture;
    private double decayRate;
    public float currentSellingPrice;
    private float sellingPriceAtHarvest;

    public InventoryItem()
    {
    }

    public InventoryItem(Plant plant, float saleMultiplier = 1.0f)
    {
        name = plant.PlantTypeName;
        texture = plant.HarvestableTexture;
        decayRate = plant.InventoryDecayRate;
        sellingPriceAtHarvest = plant.GetSaleValue * saleMultiplier;
        currentSellingPrice = sellingPriceAtHarvest;
    }

    public void Decay(double delta)
    {
        float minSellingPrice = Mathf.Min(sellingPriceAtHarvest, 1);
        currentSellingPrice = Mathf.Max(
            minSellingPrice,
            currentSellingPrice - (float)(delta * decayRate));
    }
}