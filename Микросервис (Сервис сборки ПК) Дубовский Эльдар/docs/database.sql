--
-- PostgreSQL database dump
--

\restrict semh4zcYepqFx5fRKOPVd35u8pDlj2FvWSsqPbogEvcIfzahRo9cznxaP9sUKVX

-- Dumped from database version 17.11
-- Dumped by pg_dump version 17.11

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: Configurations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Configurations" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" text NOT NULL,
    "BudgetRub" numeric(14,2) NOT NULL,
    "ItemsJson" text NOT NULL,
    "Status" text NOT NULL,
    "EvaluationJson" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "SavedAt" timestamp with time zone,
    "SavedPriceRub" numeric(14,2),
    "SavedProductsJson" text,
    "Version" integer NOT NULL,
    "CartId" uuid,
    "CartOperationKey" text NOT NULL
);


--
-- Name: Sessions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Sessions" (
    "TokenHash" text NOT NULL,
    "UserId" uuid NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL
);


--
-- Name: Users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Users" (
    "Id" uuid NOT NULL,
    "Login" character varying(80) NOT NULL,
    "PasswordHash" text NOT NULL
);


--
-- Name: Configurations PK_Configurations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Configurations"
    ADD CONSTRAINT "PK_Configurations" PRIMARY KEY ("Id");


--
-- Name: Sessions PK_Sessions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sessions"
    ADD CONSTRAINT "PK_Sessions" PRIMARY KEY ("TokenHash");


--
-- Name: Users PK_Users; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Users"
    ADD CONSTRAINT "PK_Users" PRIMARY KEY ("Id");


--
-- Name: IX_Configurations_SavedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Configurations_SavedAt" ON public."Configurations" USING btree ("SavedAt");


--
-- Name: IX_Configurations_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Configurations_UserId" ON public."Configurations" USING btree ("UserId");


--
-- Name: IX_Sessions_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Sessions_UserId" ON public."Sessions" USING btree ("UserId");


--
-- Name: IX_Users_Login; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Users_Login" ON public."Users" USING btree ("Login");


--
-- Name: Configurations FK_Configurations_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Configurations"
    ADD CONSTRAINT "FK_Configurations_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: Sessions FK_Sessions_Users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sessions"
    ADD CONSTRAINT "FK_Sessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict semh4zcYepqFx5fRKOPVd35u8pDlj2FvWSsqPbogEvcIfzahRo9cznxaP9sUKVX

