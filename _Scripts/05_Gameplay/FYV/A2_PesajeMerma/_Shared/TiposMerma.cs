
public enum JabaTipo { Huevos, Frutas, Verduras, Reprocesos, Reciclaje, MallasPlasticas, CajasReutilizables, Papaya, Ajitomate, Platano, 
    
    Lavado_Agua,
    Lavado_Papaya,
    Lavado_Desinfectante,

    // Zona DESINFECCI�N
    Desinfeccion_Jaba,
    Desinfeccion_Agua,
    Desinfeccion_Desinfectante,
    Desinfeccion_Papaya,
    Desinfeccion_Timer,

    // Zona CORTE � Exhibici�n
    Corte_Exhibicion_Papaya,
    Corte_Exhibicion_Cuchillo,
    Corte_Exhibicion_Film,
    Corte_Exhibicion_Supermarket,

    // Zona CORTE � Coctel
    Corte_Coctel_Papaya,
    Corte_Coctel_Cuchillo1,
    Corte_Coctel_Cuchillo2,
    Corte_Coctel_Bagazo,
    Corte_Coctel_Cuchillo3,
    Corte_Coctel_Taper,
    Corte_Coctel_Etiqueta,

    // Zona VITRINA (final)
    Vitrina_ProductoFinal
}

public enum ProductoTipo { Huevo, Fruta, Verdura, CajaReproceso, CajaReciclaje, MallaPlastica, CajaReutilizable, 
                            PapayaVerde, PapayaMedio, PapayaMadura, AjitomateVerde, AjitomateMedio, AjitomateMaduro, PlatanoVerde, PlatanoMedio, PlatanoMaduro,

    // Lavado
    AccionAgua,
    AccionPapaya,
    AccionDesinfectante,

    // Desinfecci�n
    AccionJaba,
    AccionTimer,

    // Corte � Exhibici�n
    AccionCuchillo,
    AccionBagazo,
    AccionFilm,
    AccionSupermarket,

    // Corte � Coctel
    AccionCuchillo1,
    AccionCuchillo2,
    AccionTaper,
    AccionEtiqueta,

    // Producto final (para vitrina)
    ProductoFinal_Bandeja,
    ProductoFinal_Taper
}

public enum MaturityLevel
{
    NoMaduro = 0,
    MedioMaduro = 1,
    Maduro = 2
}
