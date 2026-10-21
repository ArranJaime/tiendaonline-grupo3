using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaOnline.Data.Migrations
{
    /// <inheritdoc />
    public partial class MasProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Products" ("Name","Category","Description","Price","Stock","Emoji") VALUES
                -- Audio (5)
                ('Parlante Bluetooth Portátil', 'Audio', 'Altavoz resistente al agua con sonido envolvente y batería de larga duración para fiestas y viajes', 249.00, 30, '🔊'),
                ('Auriculares con Cancelación de Ruido', 'Audio', 'Audífonos over-ear inalámbricos ideales para concentrarse en el trabajo o escuchar música sin ruido exterior', 489.00, 25, '🎧'),
                ('Micrófono Condensador USB', 'Audio', 'Micrófono profesional para podcast, grabaciones de voz y transmisiones en vivo con filtro antipop', 199.00, 18, '🎙️'),
                ('Barra de Sonido para TV', 'Audio', 'Sistema de audio para el hogar con subwoofer inalámbrico que mejora el sonido de películas y series', 359.00, 15, '📻'),
                ('Amplificador de Guitarra Portátil', 'Audio', 'Mini amplificador para instrumentos musicales ideal para practicar en casa o llevar de viaje', 159.00, 12, '🎸'),

                -- Tecnología (5)
                ('Tablet Gráfica Digital', 'Tecnología', 'Tableta con lápiz óptico sensible a la presión para dibujo digital, ilustración y diseño gráfico', 320.00, 20, '📱'),
                ('Monitor Curvo 27 Pulgadas', 'Tecnología', 'Pantalla panorámica de alta resolución con panel antirreflejo para programar y trabajar con varias ventanas', 899.00, 14, '🖥️'),
                ('Hub USB-C Multipuerto', 'Tecnología', 'Adaptador con entradas HDMI, USB 3.0 y lector de tarjetas SD para conectar accesorios a tu laptop', 119.00, 45, '🔌'),
                ('Disco Sólido Externo SSD 1TB', 'Tecnología', 'Unidad de almacenamiento ultra rápida y resistente a golpes para copias de seguridad y transferencia de archivos pesados', 349.00, 28, '💾'),
                ('Router WiFi 6 de Alta Velocidad', 'Tecnología', 'Enrutador inalámbrico de doble banda con cobertura extendida para hogares con muchos dispositivos conectados', 289.00, 22, '📶'),

                -- Fotografía y Video (5)
                ('Trípode Profesional de Aluminio', 'Fotografía', 'Soporte ligero y ajustable para cámaras y teléfonos con cabezal giratorio para fotos y videos panorámicos', 149.00, 35, '📷'),
                ('Aro de Luz LED con Soporte', 'Fotografía', 'Iluminación circular con control de brillo y temperatura de color para videollamadas, streaming y creación de contenido', 89.00, 50, '💡'),
                ('Lente Objetivo 50mm f/1.8', 'Fotografía', 'Lente luminoso para retratos con desenfoque de fondo suave y excelente nitidez con poca luz', 450.00, 10, '📸'),
                ('Estabilizador Gimbal para Celular', 'Fotografía', 'Cardán de 3 ejes con seguimiento facial automático para grabar videos fluidos sin temblores al caminar', 379.00, 16, '📹'),
                ('Kit de Fondos para Fotografía', 'Fotografía', 'Estructura con telas intercambiables blanca, negra y verde para sesiones fotográficas y chroma key en estudio', 169.00, 12, '🎬'),

                -- Hogar y Oficina (5)
                ('Silla Ergonómica de Oficina', 'Hogar y Oficina', 'Asiento con soporte lumbar regulable y malla transpirable para largas jornadas de trabajo sentado', 499.00, 15, '🪑'),
                ('Escritorio Elevable Automático', 'Hogar y Oficina', 'Mesa de altura ajustable con motor eléctrico para alternar entre trabajar de pie y sentado', 1150.00, 8, '🛋️'),
                ('Lámpara de Escritorio con Carga Inalámbrica', 'Hogar y Oficina', 'Lámpara LED con temporizador y base de carga rápida para teléfonos móviles', 129.00, 40, '💡'),
                ('Organizador de Cables Magnético', 'Hogar y Oficina', 'Set de clips para mantener ordenados los cables de cargadores y periféricos sobre la mesa', 39.00, 60, '🗂️'),
                ('Cafetera de Goteo Programable', 'Hogar y Oficina', 'Máquina para preparar café filtrado con jarra térmica de acero inoxidable y encendido automático mañanero', 219.00, 20, '☕'),

                -- Deportes y Salud (5)
                ('Banda Elástica de Resistencia', 'Deportes y Salud', 'Set de ligas para entrenamiento de fuerza, calentamiento y terapia física en casa o gimnasio', 49.00, 70, '🏋️'),
                ('Botella Térmica de Acero Inoxidable', 'Deportes y Salud', 'Termo hermético que mantiene bebidas frías por 24 horas y calientes por 12 horas durante el ejercicio', 69.00, 55, '🧴'),
                ('Esterilla Antideslizante de Yoga', 'Deportes y Salud', 'Tapete acolchado con textura antideslizante para pilates, estiramientos y meditación', 89.00, 35, '🧘'),
                ('Pesa Rusa Kettlebell 12kg', 'Deportes y Salud', 'Mancuerna de hierro fundido para ejercicios funcionales, sentadillas y acondicionamiento cardiovascular', 119.00, 25, '💪'),
                ('Báscula Digital con Bluetooth', 'Deportes y Salud', 'Balanza inteligente que mide porcentaje de grasa corporal, masa muscular e índice de masa corporal sincronizado con el móvil', 99.00, 30, '⚖️'),

                -- Gaming (5)
                ('Ratón Gamer Ultraligero', 'Gaming', 'Mouse con sensor óptico de alta precisión, switches mecánicos y cable paracord para juegos competitivos', 179.00, 32, '🖱️'),
                ('Pad Mouse Extendido XXL', 'Gaming', 'Alfombrilla gigante de tela con bordes cosidos y base de goma para deslizar teclado y ratón con comodidad', 65.00, 45, '🎮'),
                ('Mando Inalámbrico para PC y Consola', 'Gaming', 'Controlador ergonómico con botones remapeables y vibración háptica para videojuegos de acción y carreras', 229.00, 24, '🕹️'),
                ('Soporte de Auriculares con Iluminación RGB', 'Gaming', 'Base para colgar audífonos con puertos USB integrados y efectos de luces personalizables', 79.00, 30, '🎧'),
                ('Volante de Carreras con Pedales', 'Gaming', 'Simulador de conducción con retroalimentación de fuerza y cambio de marchas para juegos de autos', 799.00, 6, '🏎️'),

                -- Libros y Educación (5)
                ('Libro de Arquitectura Cloud en AWS', 'Libros y Educación', 'Guía completa sobre diseño de infraestructuras escalables, alta disponibilidad y microservicios en la nube', 115.00, 40, '📘'),
                ('Manual de Scrum y Gestión Ágil', 'Libros y Educación', 'Texto práctico para certificar como Scrum Master y liderar equipos de desarrollo de software', 85.00, 50, '📖'),
                ('Curso de Algoritmos y Estructuras de Datos', 'Libros y Educación', 'Manual con explicaciones claras y código para preparar entrevistas técnicas de programación', 95.00, 35, '📚'),
                ('Cuaderno Digital Reutilizable', 'Libros y Educación', 'Libreta inteligente para tomar notas a mano y escanearlas directamente a la nube borrando las páginas con agua', 75.00, 40, '📝'),
                ('Guía de Buenas Prácticas en C# y .NET', 'Libros y Educación', 'Libro de referencia sobre patrones de diseño, principios SOLID y optimización de rendimiento en .NET 8', 125.00, 30, '📗');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "Products" WHERE "Name" IN (
                    'Parlante Bluetooth Portátil', 'Auriculares con Cancelación de Ruido', 'Micrófono Condensador USB', 'Barra de Sonido para TV', 'Amplificador de Guitarra Portátil',
                    'Tablet Gráfica Digital', 'Monitor Curvo 27 Pulgadas', 'Hub USB-C Multipuerto', 'Disco Sólido Externo SSD 1TB', 'Router WiFi 6 de Alta Velocidad',
                    'Trípode Profesional de Aluminio', 'Aro de Luz LED con Soporte', 'Lente Objetivo 50mm f/1.8', 'Estabilizador Gimbal para Celular', 'Kit de Fondos para Fotografía',
                    'Silla Ergonómica de Oficina', 'Escritorio Elevable Automático', 'Lámpara de Escritorio con Carga Inalámbrica', 'Organizador de Cables Magnético', 'Cafetera de Goteo Programable',
                    'Banda Elástica de Resistencia', 'Botella Térmica de Acero Inoxidable', 'Esterilla Antideslizante de Yoga', 'Pesa Rusa Kettlebell 12kg', 'Báscula Digital con Bluetooth',
                    'Ratón Gamer Ultraligero', 'Pad Mouse Extendido XXL', 'Mando Inalámbrico para PC y Consola', 'Soporte de Auriculares con Iluminación RGB', 'Volante de Carreras con Pedales',
                    'Libro de Arquitectura Cloud en AWS', 'Manual de Scrum y Gestión Ágil', 'Curso de Algoritmos y Estructuras de Datos', 'Cuaderno Digital Reutilizable', 'Guía de Buenas Prácticas en C# y .NET'
                );
                """);
        }
    }
}
