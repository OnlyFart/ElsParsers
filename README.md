# ElsParsers
Парсеры популярных электронно-библиотечных систем

* [Net.core 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) 
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
	"_id": {
		"$numberLong": "4"
	},
	"Authors": "Капнист В. В.",
	"ISBN": "9785998923784",
	"ISSN": null,
	"Publisher": "Директ-Медиа",
	"Name": "Aнтигона. Металог трагедии \"Гиневра\" : драматургия: художественная литература",
	"Year": "2010",
	"Bib": "Капнист, В.В. Aнтигона. Металог трагедии \"Гиневра\": драматургия / В.В.&nbsp;Капнист. – Москва : Директ-Медиа, 2010. – 112 с. – Режим доступа: по подписке. – URL: <a href='https://biblioclub.ru/index.php?page=book&id=4'>https://biblioclub.ru/index.php?page=book&id=4</a> (дата обращения: 13.01.2021). – ISBN 9785998923784. – Текст : электронный.<!--T--><!--T-->",
	"Pages": {
		"$numberInt": "112"
	},
	"Disciple": "Русская литература; ﻿Архитектура зданий и сооружений. Творческие концепции архитектурной деятельности"
}
```
