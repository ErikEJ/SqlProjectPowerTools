```mermaid
erDiagram
  "dbo.Album" {
    AlbumId int PK
    Title nvarchar(160) 
    ArtistId int FK
    Valid bit(NULL) 
  }
  "dbo.Album" }o--|| "dbo.Artist" : FK_AlbumArtistId
  "dbo.Artist" {
    ArtistId int PK
    Name nvarchar(120)(NULL) 
  }
  "dbo.Customer" {
    CustomerId int PK
    FirstName nvarchar(40) 
    LastName nvarchar(20) 
    Company nvarchar(80)(NULL) 
    Address nvarchar(70)(NULL) 
    City nvarchar(40)(NULL) 
    State nvarchar(40)(NULL) 
    Country nvarchar(40)(NULL) 
    PostalCode nvarchar(10)(NULL) 
    Phone nvarchar(24)(NULL) 
    Fax nvarchar(24)(NULL) 
    Email nvarchar(60) 
    SupportRepId int(NULL) FK
  }
  "dbo.Customer" }o--o| "dbo.Employee" : FK_CustomerSupportRepId
  "dbo.Employee" {
    EmployeeId int PK
    LastName nvarchar(20) 
    FirstName nvarchar(20) 
    Title nvarchar(30)(NULL) 
    ReportsTo int(NULL) FK
    BirthDate datetime(NULL) 
    HireDate datetime(NULL) 
    Address nvarchar(70)(NULL) 
    City nvarchar(40)(NULL) 
    State nvarchar(40)(NULL) 
    Country nvarchar(40)(NULL) 
    PostalCode nvarchar(10)(NULL) 
    Phone nvarchar(24)(NULL) 
    Fax nvarchar(24)(NULL) 
    Email nvarchar(60)(NULL) 
  }
  "dbo.Employee" }o--o| "dbo.Employee" : FK_EmployeeReportsTo
  "dbo.Genre" {
    GenreId int PK
    Name nvarchar(120)(NULL) 
  }
  "dbo.Invoice" {
    InvoiceId int PK
    CustomerId int FK
    InvoiceDate datetime 
    BillingAddress nvarchar(70)(NULL) 
    BillingCity nvarchar(40)(NULL) 
    BillingState nvarchar(40)(NULL) 
    BillingCountry nvarchar(40)(NULL) 
    BillingPostalCode nvarchar(10)(NULL) 
    Total numeric(10-2) 
  }
  "dbo.Invoice" }o--|| "dbo.Customer" : FK_InvoiceCustomerId
  "dbo.InvoiceLine" {
    InvoiceLineId int PK
    InvoiceId int FK
    TrackId int FK
    UnitPrice numeric(10-2) 
    Quantity int 
  }
  "dbo.InvoiceLine" }o--|| "dbo.Invoice" : FK_InvoiceLineInvoiceId
  "dbo.InvoiceLine" }o--|| "dbo.Track" : FK_InvoiceLineTrackId
  "dbo.MediaType" {
    MediaTypeId int PK
    Name nvarchar(120)(NULL) 
  }
  "dbo.Playlist" {
    PlaylistId int PK
    Name nvarchar(120)(NULL) 
  }
  "dbo.PlaylistTrack" {
    PlaylistId int PK,FK
    TrackId int PK,FK
  }
  "dbo.PlaylistTrack" }o--|| "dbo.Playlist" : FK_PlaylistTrackPlaylistId
  "dbo.PlaylistTrack" }o--|| "dbo.Track" : FK_PlaylistTrackTrackId
  "dbo.Track" {
    TrackId int PK
    Name nvarchar(200) 
    AlbumId int(NULL) FK
    MediaTypeId int FK
    GenreId int(NULL) FK
    Composer nvarchar(220)(NULL) 
    Milliseconds int 
    Bytes int(NULL) 
    UnitPrice numeric(10-2) 
  }
  "dbo.Track" }o--o| "dbo.Album" : FK_TrackAlbumId
  "dbo.Track" }o--o| "dbo.Genre" : FK_TrackGenreId
  "dbo.Track" }o--|| "dbo.MediaType" : FK_TrackMediaTypeId
```
