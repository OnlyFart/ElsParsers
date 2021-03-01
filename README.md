# ElsParsers
Парсеры популярных электронно-библиотечных систем

* [.net 5](https://dotnet.microsoft.com/download/dotnet/5.0) 
* [MongoDb](https://www.mongodb.com/)

## Пример вызова сервиса
```
uraitparser --cs mongodb://localhost:27017 --th 10 --proxy 127.0.0.1:8888
```

## Где 
```
--proxy - прокси сервер для обращения в сайту;
--cs - строка подключения к MongoDB;
--th - кол-во потоков для обращения к сайту;
```

## Полный список опций 

```
uraitparser --help
```

## Дополнительная информация 
В данном репозитории содержатся парсеры следующих библиотечных систем:
* [IPR Books](http://www.iprbookshop.ru/)
* [Urait](https://urait.ru/)
* [Лань](https://lanbook.com/)
* [Znanium](https://znanium.com/)
* [Biblioclub](https://biblioclub.ru/)
* [Академия](https://academia-moscow.ru/)
* [IBooks](https://ibooks.ru/)
* [Book.ru](https://book.ru/)
* [RuCont](https://lib.rucont.ru/) - только каталог книг
* [BiblioRossica](http://www.bibliorossica.com/)
* [StudentLibrary](https://www.studentlibrary.ru/)

Для работы каждого из парсеров необходима база данных [MongoDb](https://www.mongodb.com/).
При повторных запусках парсеров будут дособраны книги, которые отсутствуют на текущий момент в базе

## Публикация
```
dotnet publish -c Release -o Binary/win-x64 -r win-x64 --self-contained true
dotnet publish -c Release -o Binary/linux-x64 -r linux-x64 --self-contained true
```

## Пример собранных данных
```json
{ 
    "ExternalId" : "133", 
    "ElsName" : "BiblioClub", 
    "Authors" : "Писемский А. Ф.", 
    "ISBN" : "978-5-9989-2438-5", 
    "ISSN" : null, 
    "Publisher" : "Директ-Медиа", 
    "Name" : "Хищники : комедия в пяти действиях: художественная литература", 
    "Year" : "2010", 
    "Bib" : "Писемский, А.Ф. Хищники: комедия в пяти действиях : [12+] / А.Ф.&nbsp;Писемский. – Москва : Директ-Медиа, 2010. – 103 с. – Режим доступа: по подписке. – URL: <a href='https://biblioclub.ru/index.php?page=book&id=133'>https://biblioclub.ru/index.php?page=book&id=133</a> (дата обращения: 06.02.2021). – ISBN 978-5-9989-2438-5. – Текст : электронный.<!--T--><!--T-->", 
    "Pages" : 103
}
```
