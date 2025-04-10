using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f; // Geschwindigkeit der Bewegung

    // Arrays für die Walking-Sprites
    public Sprite[] walkDownSprites;   // 6 Sprites für das Gehen nach unten
    public Sprite[] walkUpSprites;     // 6 Sprites für das Gehen nach oben
    public Sprite[] walkSideSprites;   // 5 Sprites für das Gehen seitlich (links und rechts)

    // Arrays für die Idle-Sprites
    public Sprite idleDownSprite;   // Idle-Sprite für das Stehen nach unten
    public Sprite idleUpSprite;     // Idle-Sprite für das Stehen nach oben
    public Sprite idleSideSprite;   // Idle-Sprite für das Stehen seitlich

    private Rigidbody2D rb;
    private SpriteRenderer sr;   // SpriteRenderer für das Wechseln der Sprites

    private int currentSpriteIndex = 0;  // Index des aktuellen Walking-Sprites
    private float walkAnimationTime = 0.1f; // Zeit zwischen den Sprite-wechseln (für Animationen)
    private float timeSinceLastChange = 0f; // Zeit seit der letzten Änderung des Sprites

    private bool isMoving = false; // Variable für die Bewegungserkennung
    private Vector2 lastDirection = Vector2.zero; // Speichert die letzte Bewegungsrichtung

    // Start wird einmalig aufgerufen, wenn das Skript startet
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();  // Rigidbody2D holen
        sr = GetComponent<SpriteRenderer>(); // SpriteRenderer holen
    }

    // Update wird einmal pro Frame aufgerufen
    void Update()
    {
        // Bewegung auf der Y-Achse (nach oben und unten)
        float verticalInput = Input.GetAxis("Vertical");
        // Bewegung auf der X-Achse (nach links und rechts)
        float horizontalInput = Input.GetAxis("Horizontal");

        // Bewegung berechnen
        Vector2 movement = new Vector2(horizontalInput * moveSpeed, verticalInput * moveSpeed);
        rb.linearVelocity = movement; // Geschwindigkeit des Rigidbodies setzen

        // Überprüfen, ob sich der Spieler bewegt
        isMoving = horizontalInput != 0 || verticalInput != 0;

        // Speichern der letzten Richtung (auch wenn der Spieler stillsteht)
        if (isMoving)
        {
            lastDirection = movement.normalized;
        }

        // Zeit für die Animationen berechnen (um Sprites nach der festgelegten Zeit zu wechseln)
        if (isMoving)
        {
            timeSinceLastChange += Time.deltaTime;

            if (timeSinceLastChange >= walkAnimationTime)
            {
                // Zeit überschritten, Sprite wechseln
                currentSpriteIndex = (currentSpriteIndex + 1) % GetCurrentSpriteArray().Length;
                timeSinceLastChange = 0f; // Zeit zurücksetzen
            }

            // Richtige Walking-Sprites basierend auf der Eingabe auswählen
            if (verticalInput > 0) // Nach oben bewegen
            {
                sr.sprite = walkUpSprites[currentSpriteIndex]; // Walking-Sprite nach oben
            }
            else if (verticalInput < 0) // Nach unten bewegen
            {
                sr.sprite = walkDownSprites[currentSpriteIndex]; // Walking-Sprite nach unten
            }
            else if (horizontalInput != 0) // Nach links oder rechts bewegen
            {
                sr.sprite = walkSideSprites[currentSpriteIndex]; // Walking-Sprite seitlich
                sr.flipX = horizontalInput < 0; // Wenn nach links, Sprite spiegeln
            }
        }
        else
        {
            // Wenn der Charakter nicht bewegt, Idle-Sprite basierend auf der letzten Richtung
            if (lastDirection.y > 0) // Letzte Bewegung nach oben
            {
                sr.sprite = idleUpSprite; // Idle-Sprite nach oben
            }
            else if (lastDirection.y < 0) // Letzte Bewegung nach unten
            {
                sr.sprite = idleDownSprite; // Idle-Sprite nach unten
            }
            else if (lastDirection.x != 0) // Letzte Bewegung seitlich
            {
                sr.sprite = idleSideSprite; // Idle-Sprite seitlich
                sr.flipX = lastDirection.x < 0; // Wenn nach links, Sprite spiegeln
            }
            else
            {
                // Wenn keine Eingabe erfolgte und keine Bewegung, standard Idle (nach unten schauen)
                sr.sprite = idleDownSprite; // Standard Idle-Sprite nach unten
            }
        }
    }

    // Hilfsmethode, um das korrekte Array von Sprites basierend auf der Richtung auszuwählen
    Sprite[] GetCurrentSpriteArray()
    {
        // Abhängig von der Bewegungsrichtung das entsprechende Array zurückgeben
        if (Input.GetAxis("Vertical") > 0)
            return walkUpSprites;  // Gehe nach oben
        else if (Input.GetAxis("Vertical") < 0)
            return walkDownSprites; // Gehe nach unten
        else
            return walkSideSprites; // Gehe seitlich
    }
}
