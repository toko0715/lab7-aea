IF DB_ID(N'BibliotecaDB') IS NULL CREATE DATABASE BibliotecaDB;
GO
USE BibliotecaDB;
GO
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.Autores', N'U') IS NULL
BEGIN
    BEGIN TRANSACTION;

    CREATE TABLE dbo.Autores (
        AutorId int IDENTITY PRIMARY KEY, Nombre nvarchar(100) NOT NULL,
        Nacionalidad nvarchar(60) NOT NULL, Activo bit NOT NULL DEFAULT 1
    );
    CREATE TABLE dbo.Libros (
        LibroId int IDENTITY PRIMARY KEY, Titulo nvarchar(150) NOT NULL,
        ISBN nvarchar(20) NOT NULL UNIQUE, AutorId int NOT NULL REFERENCES dbo.Autores(AutorId),
        Ejemplares int NOT NULL CHECK (Ejemplares >= 0), Activo bit NOT NULL DEFAULT 1
    );
    CREATE TABLE dbo.Socios (
        SocioId int IDENTITY PRIMARY KEY, DNI nvarchar(8) NOT NULL UNIQUE,
        Nombre nvarchar(100) NOT NULL, Email nvarchar(120) NOT NULL, Activo bit NOT NULL DEFAULT 1
    );
    CREATE TABLE dbo.Prestamos (
        PrestamoId int IDENTITY PRIMARY KEY, SocioId int NOT NULL REFERENCES dbo.Socios(SocioId),
        FechaPrestamo date NOT NULL, FechaLimite date NOT NULL, Estado nvarchar(12) NOT NULL,
        CHECK (FechaLimite >= FechaPrestamo), CHECK (Estado IN (N'Pendiente', N'Devuelto'))
    );
    CREATE TABLE dbo.DetallePrestamo (
        PrestamoId int NOT NULL REFERENCES dbo.Prestamos(PrestamoId),
        LibroId int NOT NULL REFERENCES dbo.Libros(LibroId), FechaDevolucion date NULL,
        PRIMARY KEY (PrestamoId, LibroId)
    );

    INSERT dbo.Autores (Nombre, Nacionalidad) VALUES
        (N'Mario Vargas Llosa',N'Perú'), (N'Julio Ramón Ribeyro',N'Perú'),
        (N'Gabriel García Márquez',N'Colombia'), (N'Isabel Allende',N'Chile'),
        (N'Jorge Luis Borges',N'Argentina'), (N'Elena Poniatowska',N'México'),
        (N'Rosario Castellanos',N'México'), (N'José María Arguedas',N'Perú');

    INSERT dbo.Libros (Titulo, ISBN, AutorId, Ejemplares) VALUES
        (N'La ciudad y los perros',N'9780000000002',1,3),
        (N'Conversación en La Catedral',N'9780000000019',1,3),
        (N'La fiesta del Chivo',N'9780000000026',1,2),
        (N'La palabra del mudo',N'9780000000033',2,3),
        (N'Prosas apátridas',N'9780000000040',2,3),
        (N'Cien años de soledad',N'9780000000057',3,4),
        (N'El coronel no tiene quien le escriba',N'9780000000064',3,2),
        (N'Crónica de una muerte anunciada',N'9780000000071',3,3),
        (N'La casa de los espíritus',N'9780000000088',4,3),
        (N'Eva Luna',N'9780000000095',4,2),
        (N'Paula',N'9780000000101',4,2),
        (N'Ficciones',N'9780000000118',5,3),
        (N'El Aleph',N'9780000000125',5,3),
        (N'El libro de arena',N'9780000000132',5,1),
        (N'La noche de Tlatelolco',N'9780000000149',6,3),
        (N'Hasta no verte Jesús mío',N'9780000000156',6,2),
        (N'Balún Canán',N'9780000000163',7,2),
        (N'Oficio de tinieblas',N'9780000000170',7,2),
        (N'Los ríos profundos',N'9780000000187',8,3),
        (N'Todas las sangres',N'9780000000194',8,0);

    INSERT dbo.Socios (DNI, Nombre, Email) VALUES
        (N'40000001',N'Ana Torres',N'ana@example.com'),(N'40000002',N'Bruno Rojas',N'bruno@example.com'),
        (N'40000003',N'Carla Medina',N'carla@example.com'),(N'40000004',N'Diego Salas',N'diego@example.com'),
        (N'40000005',N'Elena Cruz',N'elena@example.com'),(N'40000006',N'Fabio Ruiz',N'fabio@example.com'),
        (N'40000007',N'Gabriela León',N'gabriela@example.com'),(N'40000008',N'Hugo Díaz',N'hugo@example.com'),
        (N'40000009',N'Inés Vega',N'ines@example.com'),(N'40000010',N'Jorge Soto',N'jorge@example.com');

    INSERT dbo.Prestamos (SocioId, FechaPrestamo, FechaLimite, Estado) VALUES
        (1,'20261010','20261017',N'Pendiente'),(2,'20261012','20261019',N'Pendiente'),
        (3,'20261014','20261021',N'Devuelto'),(4,'20261017','20261024',N'Pendiente'),
        (5,'20261020','20261027',N'Devuelto');
    INSERT dbo.DetallePrestamo (PrestamoId, LibroId, FechaDevolucion) VALUES
        (1,1,NULL),(1,4,NULL),(1,6,NULL),(2,9,NULL),(3,12,'20261020'),(4,15,NULL),(5,19,'20261027');

    UPDATE l SET Ejemplares = l.Ejemplares - pendientes.Cantidad
    FROM dbo.Libros l INNER JOIN (
        SELECT LibroId,COUNT(*) Cantidad FROM dbo.DetallePrestamo WHERE FechaDevolucion IS NULL GROUP BY LibroId
    ) pendientes ON pendientes.LibroId=l.LibroId;

    COMMIT TRANSACTION;
END;
GO
SELECT (SELECT COUNT(*) FROM dbo.Autores) Autores, (SELECT COUNT(*) FROM dbo.Libros) Libros,
       (SELECT COUNT(*) FROM dbo.Socios) Socios, (SELECT COUNT(*) FROM dbo.Prestamos) Prestamos,
       (SELECT COUNT(*) FROM dbo.DetallePrestamo) Detalles;
