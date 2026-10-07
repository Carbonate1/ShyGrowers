using Godot;

public partial class InventoryItem : Resource
{
    public string name;
    public Texture2D texture;
    public float currentSellingPrice;
    public float sellingPriceAtHarvest;
    public double decayTime;

    public InventoryItem()
    {
    }

    public InventoryItem(Plant plant)
    {
        name = plant.PlantTypeName;
        texture = plant.HarvestableTexture;
        sellingPriceAtHarvest = plant.GetSaleValue;
        currentSellingPrice = sellingPriceAtHarvest;
        decayTime = plant.DecayTime;
    }

    public void Decay(double delta)
    {
        currentSellingPrice = Mathf.Max(
            0,
            currentSellingPrice - (float)(sellingPriceAtHarvest * delta / decayTime));
    }
}