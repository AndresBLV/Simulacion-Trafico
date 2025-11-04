using UnityEngine;
using UnityEditor;

public class TextureCreator : MonoBehaviour
{
    [MenuItem("Tools/Create Rain Texture")]
    public static void CreateRainTexture()
    {
        // Configuración de la textura
        int width = 4;
        int height = 8;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        
        // Limpiar la textura (hacerla completamente transparente)
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear; // Transparente
        }
        
        // Dibujar la gota de lluvia (línea vertical blanca)
        for (int y = 1; y < height - 1; y++)
        {
            // Centro de la textura
            int centerX = width / 2;
            
            // Hacer más ancha en el centro si es una textura más grande
            if (width >= 4)
            {
                pixels[y * width + centerX - 1] = Color.white;
                pixels[y * width + centerX] = Color.white;
                if (width > 4)
                {
                    pixels[y * width + centerX + 1] = Color.white;
                }
            }
            else
            {
                pixels[y * width + centerX] = Color.white;
            }
        }
        
        // Aplicar los píxeles
        texture.SetPixels(pixels);
        texture.Apply();
        
        // Guardar como archivo
        byte[] bytes = texture.EncodeToPNG();
        string path = "Assets/RainDropTexture.png";
        System.IO.File.WriteAllBytes(path, bytes);
        
        // Actualizar la base de datos de assets
        AssetDatabase.Refresh();
        
        Debug.Log("Textura de lluvia creada en: " + path);
    }
}