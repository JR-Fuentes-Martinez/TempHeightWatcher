# TempHeightWatcher
### A diferential approach to climate data    

Spanish (better english translation to do)  

### Sobre el concepto  
El presente proyecto se está desarrollando para tratar de definir un método de tratamiento de datos, climáticos en especial, donde el énfasis se trata de situar en como es el cambio en el tiempo de esos datos tabulados. Es sin duda la cuestión a preguntarse y responder ante la constatación de que ese cambio o transformación existe, es, ha sido y será. Y por tanto se acepta que es mejor conocer ese devenir más que por medios diferenciales. Y se propone por tanto que tales datos son la manifestación de una definición en cada instante.   

A su vez, se asume como principio distintivo que ese cambio es el que de alguna manera cumple unas leyes y es donde habría que buscarlas, siendo esas definiciones instantáneas un producto y no una causa en sí mismas.   
     
### Sobre el método  
Y para obtener un método de obtención de resultados, se asume de entrada que esa función dentro del cálculo diferencial que es la derivada, tiene una intención en principio y desde la expresión del cociente de Newton. Obtener una expresión diferencial eliminando la dependencia del incremento de tiempo. Destacamos más esto que incluso esa tendencia al límite cero que se propone, ya que es imprescindible para una consistencia dimensional cuando se intenta su representación en el espacio, en muchos caso, el papel. Nos referimos a una integridad lineal y polar.   

Y para su implementación, se incorporará series de datos obtenidos de ERA-5 CDS (Copernicus Data Store) y algunos paquetes Nuget auxiliares, que en su momento se publicarán para cumplir sus licencias. Son especialmente librerías .net para desarrollar splines cúbicas y sus derivadas, que es por lo que ha sido elegido como método para su obtención. Nos interesa especialmente esas derivadas de funciones cúbicas, suponiendo además que hay continuidad en ese cambio que no para. Tambien un programa de visualización de funciones especialmente. No interesa de momento otro método de visualización.

----------
### Objetivos y Estado  
Se citan a continuación los principales objetivos y el estado de consecución:  

1. Conseguir una automatización de los scripts de Python para la cdsapi sensible a cambios de mes. Se ha solucionado realizando peticiones por año y por mes, con los días a considerar por cada mes. Se añade una manera de captar si los registros ya existen y no necesitan ser descargados de nuevo. Está terminado y en estado de prueba.  
2. El primer objetivo aprovechando el ordenamiento de dimensiones realizado, es obtener la variación (derivada) por cada fecha de la temperatura según alturas. La variable de referencia es esa altura y sobre ella se deriva. Puede mostrar curvas interesantes en un sentido vertical y con comparación de fechas. Ahora mismo se está trabajando en esto. 
3. El segundo objetivo es tratar suficientemente el dataset y poder obtener la derivada a las distintas alturas en el tiempo. Esta respondería a una función temporal. Se espera empezar en el menor tiempo posible.
4. Las entradas al programa consisten en una serie de trabajos con unas determinadas fechas de comienzo y fin que se solicitan y que se guardan en la misma base de datos. Se da la posibilidad de descargar cada trabajo por partes y se hace un seguimiento de su estado de consecución.
5. Se considerará esa característica de las splines de ser algo indefenidas en sus puntos de inicio y final. Se tratará en todo lo posible de considerar suficiente número de valores tratando de disminuir ese espacio de indefinición a un mínimo. Tambien se ensayaran las distintas posibilidades de construcción al respecto, natural, cuadrática o con valores concretos de primera o segunda derivada.
----------
**ENGLISH AUTOMATED TRANSLATION. (sorry)**    
**About the concept**   
This project is being developed to try to define a method of data processing, especially climatic data, where the emphasis is on how the change in time of these tabulated data is. It is undoubtedly the question to be asked and answered in the face of the realization that this change or transformation exists, is, has been and will be. And therefore it is accepted that it is only possible to know this becoming by differential means. And it is therefore proposed that such data are the manifestation of a definition at each moment.   
At the same time, it is assumed as a distinctive principle that this change is the one that in some way complies with laws and is where they should be sought, these instantaneous definitions being a product and not a cause in themselves.  
   
**About the method**    
And to obtain a method of obtaining results, it is assumed from the outset that this function within the differential calculus, which is the derivative, has an intention in principle and from the expression of Newton's quotient. Obtain a differential function by eliminating the dependence on the time increment. We emphasize this more than even that tendency to zero limit that is proposed, since it is essential for dimensional consistency when trying to represent it in space, in many cases, paper. We are referring to a linear and polar integrity.   

And for its implementation, data and software obtained from ERA-5 CDS (Copernicus Data Store) and some auxiliary Nuget packages will be incorporated, which will be published in due course to comply with their licenses. They are especially .net libraries to develop cubic splines and their derivatives, which is why it has been chosen as a method for obtaining them. We are especially interested in those derivatives from cubic functions, assuming that there is continuity in this change that does not stop. Also and specially a function display program. There is no interest in another method of visualization at the moment.    

**Objectives and State**   
1. Achieve automation of Python scripts for the cdsapi sensitive to month changes. It has been solved by making requests by year and by month, with the days to be considered for each month. A way is added to capture if the records already exist and do not need to be downloaded again. It is finished and in a state of testing. 
2. The first objective, taking advantage of the ordering of 
 dimensions carried out, is to obtain the variation (derivative) for each date of the temperature according to heights. The reference variable is that height and it is derived from it. It can show interesting curves in a vertical direction and with comparison of dates. Right now we are working on this.   
3. The second objective is to sufficiently treat the dataset and to be able to obtain the derivative at the different heights in time. This would respond to a time function. It is expected to start in the shortest possible time. 
4. The entries to the program consist of a series of works with certain start and end dates that are requested and that are saved in the same database. It is possible to download each work in parts and its status of achievement is tracked.
5. That characteristic of the splines will be considered to be somewhat defenseless at their start and end points. It will be tried as much as possible to consider a sufficient number of values, trying to reduce this space of uncertainty to a minimum. The different possibilities of construction will also be tested, natural, quadratic or with specific values of first or second derivative.
----------
*"It is not only about what. Why, how and when matters."*   
*"The main goal, the truth and only the truth. Meanwhile welcome to certainty."*   
JRFM - 2025
