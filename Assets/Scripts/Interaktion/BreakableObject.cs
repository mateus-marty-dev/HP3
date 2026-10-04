using UnityEngine;

public class BreakableObject : MonoBehaviour
{
    [Header("Durability")]
    [SerializeField] private int hitsToBreak = 3;

    [Header("Physical Impact")]
    [SerializeField] private float impactMultiplier = 1f;

    [Header("World State")]
    [SerializeField] private WorldStateManager worldStateManager;
    [SerializeField] private float worldStatePenalty = -5f;

    private int currentHits = 0;
    private bool isBroken = false;


    // Wird vom HammerTool ausgelesen.
    // Damit kann jedes Objekt unterschiedlich stark
    // auf einen Hammerschlag reagieren.
    public float ImpactMultiplier => impactMultiplier;


    private void Start()
    {
        // WorldStateManager automatisch suchen,
        // falls er nicht im Inspector eingetragen wurde.
        if (worldStateManager == null)
        {
            worldStateManager =
                FindFirstObjectByType<WorldStateManager>();
        }
    }


    // Wird vom HammerTool aufgerufen,
    // wenn das Objekt getroffen wurde.
    public void Hit()
    {
        if (isBroken)
            return;


        currentHits++;


        Debug.Log(
            gameObject.name +
            " getroffen: " +
            currentHits +
            "/" +
            hitsToBreak
        );


        // Genügend Treffer erhalten?
        if (currentHits >= hitsToBreak)
        {
            Break();
        }
    }


    private void Break()
    {
        if (isBroken)
            return;


        isBroken = true;


        Debug.Log(
            gameObject.name +
            " wurde zerstört!"
        );


        // -----------------------------------------
        // WORLD STATE
        // -----------------------------------------

        if (worldStateManager != null)
        {
            worldStateManager.ChangeWorldState(
                worldStatePenalty
            );
        }


        // -----------------------------------------
        // OBJEKT ZERSTÖREN
        // -----------------------------------------

        Destroy(gameObject);
    }
}