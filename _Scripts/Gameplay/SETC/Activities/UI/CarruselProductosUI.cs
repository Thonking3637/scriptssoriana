using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CarruselProductosUI: MonoBehaviour
{
    [System.Serializable]
    public class ProductoUI
    {
        public string id;
        public Sprite icono;
    }

    public Transform contenedorCarrusel;
    public GameObject prefabItemCarrusel;
    public List<ProductoUI> productosDisponibles;

    public ReposicionNoRecolectadoActivity actividad;

    private void Start()
    {
        GenerarCarrusel();
    }

    public void GenerarCarrusel()
    {
        // Limpia el carrusel antes de generar uno nuevo
        foreach (Transform hijo in contenedorCarrusel)
            Destroy(hijo.gameObject);

        // Instancia cada producto en el carrusel
        foreach (var producto in productosDisponibles)
        {
            var item = Instantiate(prefabItemCarrusel, contenedorCarrusel);

            // 🔽 Busca al hijo llamado "Image" (el hijo real, no el padre)
            Transform hijoImagen = item.transform.Find("Image");
            if (hijoImagen != null)
            {
                Image image = hijoImagen.GetComponent<Image>();
                if (image != null)
                    image.sprite = producto.icono;
                else
                    Debug.LogWarning($"El hijo 'Image' no tiene componente Image en {item.name}.");
            }
            else
            {
                Debug.LogWarning($"No se encontró un hijo llamado 'Image' dentro de {item.name}.");
            }

            // Agrega el script arrastrable
            var arrastrable = item.gameObject.AddComponent<CarruselProductoArrastrable>();
            arrastrable.productoId = producto.id;

            // Cuando se suelta correctamente, se destruye el item
            arrastrable.onDropCorrecto += () =>
            {
                Destroy(item);
                productosDisponibles.Remove(producto);
                actividad.OnProductoColocado();
            };

        }
    }


}
