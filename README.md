# ElsParsers
Парсеры популярных электронно-библиотечных систем

* [Net.core 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) 
* [MongoDb](https://www.mongodb.com/)

## Пример вызова сервиса
```
uraitparser --proxy 127.0.0.1:8888 --cs "mongodb+srv://<login>:<pass>@<server>?retryWrites=true&w=majority" --th 10
```

## Где 
```
--proxy - прокси сервер для обращения в сайту
--cs - строка подключения к MongoDB (необходимо подставить свои значения вместо <login>, <pass>, <server>)
--th - кол-во потоков для обращения к сайту
```

## Полный список опций 

```
uraitparser --help
```

## Дополнительная информация 
В данном репозитории содержатся следующие парсеры следующих библиотечных систем:
* [IPR Books](http://www.iprbookshop.ru/)
* [Urait](https://urait.ru/)
* [Лань](https://lanbook.com/)
* [Znarium](https://znanium.com/)
* [Biblioclub](https://biblioclub.ru/)
* [Академия](https://academia-moscow.ru/)
* [IBooks](https://ibooks.ru/)

Для работы каждого из парсеров необходима база данных [MongoDb](https://www.mongodb.com/).
При повторных запусках парсеров будут дособраны книги, которые отсутствуют на текущий момент в базе

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
