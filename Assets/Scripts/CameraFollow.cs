using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;  // Der Transform des Spielers, dem die Kamera folgen soll
    public float smoothSpeed = 0.125f;  // Die Geschwindigkeit, mit der die Kamera folgen soll
    public Vector3 offset;  // Der Offset, um die Kamera hinter oder über den Charakter zu positionieren

    void LateUpdate()
    {
        // Berechne die gewünschte Position der Kamera (mit Offset), nur X und Y bewegen sich
        Vector3 desiredPosition = new Vector3(player.position.x + offset.x, player.position.y + offset.y, transform.position.z);

        // Berechne die glatte Bewegung der Kamera
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Setze die Kamera auf die berechnete Position
        transform.position = smoothedPosition;
    }
}