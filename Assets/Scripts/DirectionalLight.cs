using UnityEngine;

public class SunDirectionalLight : MonoBehaviour
{
    public Transform sun; // Arrastra la esfera del Sol aquí

    void Update()
    {
        if (sun == null) return;

        // La luz apunta desde el Sol hacia el centro de la escena
        transform.rotation = Quaternion.LookRotation(
            Vector3.zero - sun.position
        );
    }
}