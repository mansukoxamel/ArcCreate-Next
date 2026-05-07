#!/bin/sh
# I really don't want to install Ruby on my computer just for this so here we go
docker run --rm -v $PWD:/arccreate ruby:3.2-alpine sh -c "adduser -D user && apk add git && cd /arccreate && su user -c 'bundle lock --update'"
