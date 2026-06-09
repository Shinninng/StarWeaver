using UnityEngine;

public class PlanetCamFollow : MonoBehaviour
{
    public Transform objetivo;      // Arrastrar el Sol
    public float altura = 40000f;
    public float offsetX = 20000f;

    void LateUpdate()
    {
        // Sigue al Sol manteniendose arriba
        transform.position = new Vector3(
            objetivo.position.x + offsetX,
            objetivo.position.y + altura,
            objetivo.position.z + 20000f
        );
    }
}