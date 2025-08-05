public class CustomerRuntimeData
{
    public float CurrentPatience { get; set; }
    public Table AssignedTable { get; set; }
    public FoodData CurrentOrder { get; set; }
    public float EatingTimer { get; set; }
    
    public CustomerRuntimeData(float maxPatience, float eatingTimer)
    {
        CurrentPatience = maxPatience;
        AssignedTable = null;
        CurrentOrder = null;
        EatingTimer = eatingTimer;
    }
}
